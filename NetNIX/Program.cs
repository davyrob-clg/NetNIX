// See https://aka.ms/new-console-template for more information
using NetNIX.Scripting;
using NetNIX.Setup;
using NetNIX.Shell;
using NetNIX.Users;
using NetNIX.VFS;

// ── Determine filesystem archive path ──────────────────────────────
string dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "NetNIX");

Directory.CreateDirectory(dataDir);
string archivePath = Path.Combine(dataDir, "rootfs.zip");

Boot();

void Boot()
{
    // ── Initialise the virtual file system ─────────────────────────────
    var fs = new VirtualFileSystem(archivePath);
    fs.Load();

    var userMgr = new UserManager(fs);

    // ── Boot banner with reset option ──────────────────────────────────
    Console.WriteLine("╔════════════════════════════════════════════╗");
    Console.WriteLine("║              NetNIX Booting...             ║");
    Console.WriteLine("║    Press Ctrl+R within 3s to reset env    ║");
    Console.WriteLine("╚════════════════════════════════════════════╝");

    if (WaitForResetKey(TimeSpan.FromSeconds(3)))
    {
        if (ConfirmAndReset(fs, userMgr))
        {
            Boot();
            return;
        }
    }

    // ── First-run setup if no users exist ──────────────────────────────
    bool isFirstRun = !fs.IsFile("/etc/passwd") || fs.ReadFile("/etc/passwd").Length == 0;

    if (isFirstRun)
    {
        FirstRunSetup.Run(fs, userMgr, archivePath);
    }

    userMgr.Load();

    // ── Initialise script runner ───────────────────────────────────────
    var scriptRunner = new ScriptRunner(fs, userMgr);

    // ── Display MOTD ───────────────────────────────────────────────────
    if (fs.IsFile("/etc/motd"))
    {
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(fs.ReadFile("/etc/motd")));
    }

    // ── Login loop ─────────────────────────────────────────────────────
    while (true)
    {
        Console.WriteLine("─── NetNIX Login ───");
        Console.Write("login: ");
        string? username = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(username)) continue;

        // ── Secret reset username ──────────────────────────────────────
        if (username == "__reset__")
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
        var shell = new NixShell(fs, userMgr, scriptRunner, user);
        shell.Run();

        // After shell exits, save and show login again
        fs.Save();
        Console.WriteLine($"\n{username} logged out.\n");
    }
}

// ── Reset helpers ──────────────────────────────────────────────────
static bool WaitForResetKey(TimeSpan timeout)
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
