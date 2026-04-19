/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
// See https://aka.ms/new-console-template for more information
using NetNIX.Config;
using NetNIX.Scripting;
using NetNIX.Setup;
using NetNIX.Shell;
using NetNIX.Users;
using NetNIX.VFS;

// ── Load host-side configuration ───────────────────────────────────
var config = new NxConfig();
string archivePath = config.ResolveRootfsPath();

// Ensure the directory for the rootfs exists
Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

Boot();

void Boot()
{
    // ── Initialise the virtual file system ─────────────────────────────
    var fs = new VirtualFileSystem(archivePath);
    fs.Load();

    var userMgr = new UserManager(fs);

    // ── Boot banner ────────────────────────────────────────────────────
    if (config.ShowBootBanner)
    {
        const int boxWidth = 58; // inner width between the ║ characters
        const string border = "══════════════════════════════════════════════════════════";

        static string CenterLine(string text, int width)
        {
            if (text.Length >= width) return text[..width];
            int pad = (width - text.Length) / 2;
            return new string(' ', pad) + text + new string(' ', width - pad - text.Length);
        }

        string name = config.InstanceName;
        if (name.Length > boxWidth - 2) name = name[..(boxWidth - 2)];

        Console.WriteLine();
        Console.WriteLine($"╔{border}╗");
        Console.WriteLine($"║{CenterLine("", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("███╗   ██╗███████╗████████╗███╗   ██╗██╗██╗  ██╗", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("████╗  ██║██╔════╝╚══██╔══╝████╗  ██║██║╚██╗██╔╝", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("██╔██╗ ██║█████╗     ██║   ██╔██╗ ██║██║ ╚███╔╝ ", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("██║╚██╗██║██╔══╝     ██║   ██║╚██╗██║██║ ██╔██╗ ", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("██║ ╚████║███████╗   ██║   ██║ ╚████║██║██╔╝ ██╗", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("╚═╝  ╚═══╝╚══════╝   ╚═╝   ╚═╝  ╚═══╝╚═╝╚═╝  ╚═╝", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("A .NET-Powered Virtual UNIX Environment", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("", boxWidth)}║");
        Console.WriteLine($"╠{border}╣");
        Console.WriteLine($"║{CenterLine($"Machine: {name}", boxWidth)}║");
        Console.WriteLine($"║{CenterLine("", boxWidth)}║");
        Console.WriteLine($"║  Web:    netnix.controlfeed.info{new string(' ', boxWidth - 34)}║");
        Console.WriteLine($"║  Source: github.com/squiblez/NetNIX{new string(' ', boxWidth - 37)}║");
        Console.WriteLine($"║{CenterLine("", boxWidth)}║");
        if (config.AllowReset)
        {
            string resetLine = $"Press Ctrl+R within {config.BootTimeout}s to reset environment";
            Console.WriteLine($"║{CenterLine(resetLine, boxWidth)}║");
        }
        Console.WriteLine($"╚{border}╝");
        Console.WriteLine();
    }

    if (config.AllowReset && config.BootTimeout > 0)
    {
        if (WaitForResetKey(TimeSpan.FromSeconds(config.BootTimeout)))
        {
            if (ConfirmAndReset(fs, userMgr))
            {
                Boot();
                return;
            }
        }
    }

    // ── First-run setup if no users exist ──────────────────────────────
    bool isFirstRun = !fs.IsFile("/etc/passwd") || fs.ReadFile("/etc/passwd").Length == 0;

    if (isFirstRun)
    {
        FirstRunSetup.Run(fs, userMgr, archivePath);
    }
    else
    {
        // ── Auto-update factory files when the build changes ───────────
        // Compare the executable's last-write timestamp against a stamp
        // stored in the VFS. Only reinstall when the binary is newer.
        SyncFactoryIfBuildChanged(fs);
    }

    userMgr.Load();

    // ── Initialise script runner ───────────────────────────────────────
    var scriptRunner = new ScriptRunner(fs, userMgr);
    var daemonMgr = new NetNIX.Scripting.DaemonManager(fs, userMgr, scriptRunner);

    // ── Display MOTD ───────────────────────────────────────────────────
    if (config.MotdEnabled && fs.IsFile("/etc/motd"))
    {
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(fs.ReadFile("/etc/motd")));
    }

    // ── Login loop ─────────────────────────────────────────────────────
    while (true)
    {
        Console.WriteLine($"─── {config.LoginBanner} ───");
        Console.Write("login: ");
        string? username = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(username)) continue;

        // ── Secret reset username ──────────────────────────────────────
        if (username == "__reset__" && config.AllowReset)
        {
            if (ConfirmAndReset(fs, userMgr))
            {
                Boot();
                return;
            }
            continue;
        }

        Console.Write("password: ");
        string? password = ReadPassword();
        if (password == null) continue;

        var user = userMgr.Authenticate(username, password);
        if (user == null)
        {
            Console.WriteLine("Login incorrect.\n");
            continue;
        }

        // ── Launch shell ───────────────────────────────────────────────
        var shell = new NixShell(fs, userMgr, scriptRunner, daemonMgr, user);
        try
        {
            shell.Run();
        }
        catch (Exception ex)
        {
            Console.ResetColor();
            Console.WriteLine($"\nnsh: fatal error: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine("Session terminated. Returning to login.");
        }

        // After shell exits, save and show login again
        if (config.AutoSave)
            fs.Save();
        Console.WriteLine($"\n{username} logged out.\n");
    }
}

// ── Reset helpers ──────────────────────────────────────────────────
static bool WaitForResetKey(TimeSpan timeout)
{
    if (!Console.IsInputRedirected)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.R && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    return true;
            }
            Thread.Sleep(50);
        }
    }
    return false;
}

bool ConfirmAndReset(VirtualFileSystem fs, UserManager userMgr)
{
    Console.WriteLine();
    Console.WriteLine("╔════════════════════════════════════════════╗");
    Console.WriteLine("║  WARNING: This will erase the entire      ║");
    Console.WriteLine("║  environment and all user data!            ║");
    Console.WriteLine("╚════════════════════════════════════════════╝");
    Console.Write("Type 'YES' to confirm full reset: ");
    string? confirm = Console.ReadLine()?.Trim();

    if (confirm != "YES")
    {
        Console.WriteLine("Reset cancelled.\n");
        return false;
    }

    // Delete the archive and re-run setup
    if (File.Exists(archivePath))
        File.Delete(archivePath);

    Console.WriteLine("[*] Environment wiped.\n");
    return true;
}

// ── Masked password input ──────────────────────────────────────────
static string? ReadPassword()
{
    if (Console.IsInputRedirected)
    {
        return Console.ReadLine();
    }

    var sb = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0)
            {
                sb.Remove(sb.Length - 1, 1);
                Console.Write("\b \b");
            }
        }
        else
        {
            sb.Append(key.KeyChar);
            Console.Write('*');
        }
    }

    return sb.Length > 0 ? sb.ToString() : null;
}

// ── Build-stamp sync ───────────────────────────────────────────────
// Computes a content hash across the exe and all shipped content
// directories. Only reinstalls factory files when actual content changes,
// not when timestamps shift (antivirus, copies, etc).
void SyncFactoryIfBuildChanged(VirtualFileSystem fs)
{
    const string stampPath = "/etc/.build-stamp";

    string baseDir = AppContext.BaseDirectory;
    string exePath = Environment.ProcessPath ?? typeof(Program).Assembly.Location;

    // Build a SHA-256 hash from the content of the exe and all shipped files.
    using var sha = System.Security.Cryptography.SHA256.Create();
    using var hashStream = System.Security.Cryptography.IncrementalHash.CreateHash(
        System.Security.Cryptography.HashAlgorithmName.SHA256);

    // Hash the executable itself
    if (File.Exists(exePath))
        hashStream.AppendData(File.ReadAllBytes(exePath));

    // Hash all content directories in sorted order for determinism
    string[] contentDirs = ["Builtins", "SystemBuiltins", "Libs", "helpman", "Factory"];
    foreach (var dir in contentDirs)
    {
        string full = Path.Combine(baseDir, dir);
        if (!Directory.Exists(full)) continue;
        var files = Directory.GetFiles(full, "*", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        foreach (var file in files)
        {
            // Include the relative path so renames are detected
            string rel = Path.GetRelativePath(baseDir, file);
            hashStream.AppendData(System.Text.Encoding.UTF8.GetBytes(rel));
            hashStream.AppendData(File.ReadAllBytes(file));
        }
    }

    byte[] hashBytes = hashStream.GetHashAndReset();
    string currentStamp = Convert.ToHexString(hashBytes);

    if (fs.IsFile(stampPath))
    {
        string saved = System.Text.Encoding.UTF8.GetString(fs.ReadFile(stampPath)).Trim();
        if (saved == currentStamp)
            return; // nothing changed
    }

    // Something changed — reinstall factory content
    FirstRunSetup.InstallBuiltinScripts(fs);
    FirstRunSetup.InstallBuiltinLibs(fs);
    FirstRunSetup.InstallManPages(fs);
    FirstRunSetup.InstallFactoryFiles(fs);

    // Update sandbox exceptions with current defaults
    var exceptionsData = System.Text.Encoding.UTF8.GetBytes(NetNIX.Scripting.ScriptRunner.DefaultSandboxExceptions);
    if (fs.IsFile("/etc/sandbox.exceptions"))
        fs.WriteFile("/etc/sandbox.exceptions", exceptionsData);
    else
        fs.CreateFile("/etc/sandbox.exceptions", 0, 0, exceptionsData, "rw-r-----");

    // Update the stamp
    var stampData = System.Text.Encoding.UTF8.GetBytes(currentStamp);
    if (fs.IsFile(stampPath))
        fs.WriteFile(stampPath, stampData);
    else
        fs.CreateFile(stampPath, 0, 0, stampData, "rw-------");

    fs.Save();
}
