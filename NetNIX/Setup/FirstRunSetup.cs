using NetNIX.Users;
using NetNIX.VFS;

namespace NetNIX.Setup;

/// <summary>
/// First-run setup wizard. Initialises the virtual file system, creates the
/// standard directory tree and the root account.
/// </summary>
public static class FirstRunSetup
{
    public static void Run(VirtualFileSystem fs, UserManager userMgr, string archivePath)
    {
        Console.WriteLine("??????????????????????????????????????????????");
        Console.WriteLine("?        NetNIX — First-Time Setup          ?");
        Console.WriteLine("??????????????????????????????????????????????");
        Console.WriteLine();

        // 1. Create standard UNIX directory tree
        Console.WriteLine("[*] Creating filesystem hierarchy...");
        CreateDirectoryTree(fs);

        // 2. Install default shell startup script into /etc/skel
        //    (must happen before any user creation so CopySkelFiles works)
        Console.WriteLine("[*] Installing default startup scripts...");
        InstallSkelFiles(fs);

        // 3. Create root user
        Console.WriteLine();
        Console.Write("Choose a root password: ");
        string? rootPass = ReadPassword();
        while (string.IsNullOrWhiteSpace(rootPass))
        {
            Console.Write("Password cannot be empty. Try again: ");
            rootPass = ReadPassword();
        }

        userMgr.CreateUser("root", rootPass, "/root");
        InstallUserFiles(fs, "root", 0, 0, "/root");
        Console.WriteLine("[*] Root account created.");

        // Create the sudo group so users can be granted root privileges
        Console.WriteLine("[*] Creating sudo group...");
        userMgr.CreateGroup("sudo");

        // 4. Optionally create a regular user
        Console.WriteLine();
        Console.Write("Create a regular user? (y/n): ");
        if (Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true)
        {
            Console.Write("Username: ");
            string? username = Console.ReadLine()?.Trim();
            while (string.IsNullOrWhiteSpace(username))
            {
                Console.Write("Username cannot be empty. Try again: ");
                username = Console.ReadLine()?.Trim();
            }

            Console.Write($"Password for {username}: ");
            string? userPass = ReadPassword();
            while (string.IsNullOrWhiteSpace(userPass))
            {
                Console.Write("Password cannot be empty. Try again: ");
                userPass = ReadPassword();
            }

            userMgr.CreateUser(username, userPass);
            Console.WriteLine($"[*] User '{username}' created.");

            Console.Write($"Add {username} to sudo group? (y/n): ");
            if (Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                userMgr.AddUserToGroup(username, "sudo");
                Console.WriteLine($"[*] {username} added to sudo group.");
            }
        }

        // 5. Write welcome motd
        var motd = "Welcome to NetNIX — a .NET powered multi-user UNIX environment.\n"u8.ToArray();
        fs.CreateFile("/etc/motd", 0, 0, motd, "rw-r--r--");

        // 5b. Install sandbox configuration (root-editable script security rules)
        Console.WriteLine("[*] Installing sandbox configuration...");
        var sandboxData = System.Text.Encoding.UTF8.GetBytes(NetNIX.Scripting.ScriptRunner.DefaultSandboxConfig);
        fs.CreateFile("/etc/sandbox.conf", 0, 0, sandboxData, "rw-r-----");

        var exceptionsData = System.Text.Encoding.UTF8.GetBytes(NetNIX.Scripting.ScriptRunner.DefaultSandboxExceptions);
        fs.CreateFile("/etc/sandbox.exceptions", 0, 0, exceptionsData, "rw-r-----");

        // 6. Install built-in script commands
        Console.WriteLine("[*] Installing built-in commands...");
        InstallBuiltinScripts(fs);

        // 7. Install built-in libraries
        Console.WriteLine("[*] Installing built-in libraries...");
        InstallBuiltinLibs(fs);

        // 8. Install manual pages
        Console.WriteLine("[*] Installing manual pages...");
        InstallManPages(fs);

        // 9. Install factory files
        Console.WriteLine("[*] Installing factory files...");
        InstallFactoryFiles(fs);

        // 10. Save everything
        fs.Save();
        Console.WriteLine();
        Console.WriteLine("[*] Setup complete! Filesystem saved to: " + archivePath);
        Console.WriteLine();
    }

    private static void CreateDirectoryTree(VirtualFileSystem fs)
    {
        // ?? Standard FHS directories (owner=root, group=root) ??????
        // Format: (path, ownerId, groupId, permissions)
        (string path, int uid, int gid, string perms)[] dirs =
        [
            // Root-level directories
            ("/bin",            0, 0, "rwxr-xr-x"),
            ("/boot",           0, 0, "rwxr-xr-x"),
            ("/dev",            0, 0, "rwxr-xr-x"),
            ("/etc",            0, 0, "rwxr-xr-x"),
            ("/etc/default",    0, 0, "rwxr-xr-x"),
            ("/etc/opt",        0, 0, "rwxr-xr-x"),
            ("/etc/skel",       0, 0, "rwxr-xr-x"),
            ("/etc/sysconfig",  0, 0, "rwxr-xr-x"),
            ("/home",           0, 0, "rwxr-xr-x"),
            ("/lib",            0, 0, "rwxr-xr-x"),
            ("/lib64",          0, 0, "rwxr-xr-x"),
            ("/media",          0, 0, "rwxr-xr-x"),
            ("/mnt",            0, 0, "rwxr-xr-x"),
            ("/opt",            0, 0, "rwxr-xr-x"),
            ("/proc",           0, 0, "r-xr-xr-x"),
            ("/root",           0, 0, "rwx------"),
            ("/run",            0, 0, "rwxr-xr-x"),
            ("/sbin",           0, 0, "rwxr-xr-x"),
            ("/srv",            0, 0, "rwxr-xr-x"),
            ("/sys",            0, 0, "r-xr-xr-x"),
            ("/tmp",            0, 0, "rwxrwxrwt"),

            // /usr hierarchy
            ("/usr",            0, 0, "rwxr-xr-x"),
            ("/usr/bin",        0, 0, "rwxr-xr-x"),
            ("/usr/include",    0, 0, "rwxr-xr-x"),
            ("/usr/lib",        0, 0, "rwxr-xr-x"),
            ("/usr/lib64",      0, 0, "rwxr-xr-x"),
            ("/usr/local",      0, 0, "rwxr-xr-x"),
            ("/usr/local/bin",  0, 0, "rwxr-xr-x"),
            ("/usr/local/etc",  0, 0, "rwxr-xr-x"),
            ("/usr/local/include", 0, 0, "rwxr-xr-x"),
            ("/usr/local/lib",  0, 0, "rwxr-xr-x"),
            ("/usr/local/sbin", 0, 0, "rwxr-xr-x"),
            ("/usr/local/share",0, 0, "rwxr-xr-x"),
            ("/usr/local/src",  0, 0, "rwxr-xr-x"),
            ("/usr/sbin",       0, 0, "rwxr-xr-x"),
            ("/usr/share",      0, 0, "rwxr-xr-x"),
            ("/usr/share/doc",  0, 0, "rwxr-xr-x"),
            ("/usr/share/man",  0, 0, "rwxr-xr-x"),
            ("/usr/share/misc", 0, 0, "rwxr-xr-x"),
            ("/usr/src",        0, 0, "rwxr-xr-x"),

            // /var hierarchy
            ("/var",            0, 0, "rwxr-xr-x"),
            ("/var/cache",      0, 0, "rwxr-xr-x"),
            ("/var/lib",        0, 0, "rwxr-xr-x"),
            ("/var/lock",       0, 0, "rwxrwxrwt"),
            ("/var/log",        0, 0, "rwxr-xr-x"),
            ("/var/mail",       0, 0, "rwxrwxrwt"),
            ("/var/opt",        0, 0, "rwxr-xr-x"),
            ("/var/run",        0, 0, "rwxr-xr-x"),
            ("/var/spool",      0, 0, "rwxr-xr-x"),
            ("/var/spool/cron", 0, 0, "rwxr-xr-x"),
            ("/var/spool/mail", 0, 0, "rwxrwxrwt"),
            ("/var/tmp",        0, 0, "rwxrwxrwt"),

            // Package manager
            ("/var/lib/npak",   0, 0, "rwxr-xr-x"),

            // Web server
            ("/var/www",        0, 0, "rwxr-xr-x"),
        ];

        foreach (var (path, uid, gid, perms) in dirs)
        {
            if (!fs.Exists(path))
                fs.CreateDirectory(path, uid, gid, perms);
        }
    }

    private static string BuildNshrc(string username, int uid)
    {
        return $"""
            # ~/.nshrc — NetNIX Shell startup script
            # This file runs automatically when you log in.
            # Edit it with: edit ~/.nshrc

            # Welcome message
            echo "Welcome back, {username} (uid={uid})!"
            echo ""

            # Show message of the day
            cat /etc/motd

            # Display current date
            date
            echo ""
            echo "Type 'help' for commands or 'man <topic>' for detailed help."
            echo ""
            """;
    }

    private static void InstallSkelFiles(VirtualFileSystem fs)
    {
        // Install a generic .nshrc template into /etc/skel.
        // $USER / $UID are expanded at runtime by the shell.
        const string skelNshrc = """
            # ~/.nshrc — NetNIX Shell startup script
            # This file runs automatically when you log in.
            # Edit it with: edit ~/.nshrc

            # Welcome message
            echo "Welcome back, $USER (uid=$UID)!"
            echo ""

            # Show message of the day
            cat /etc/motd

            # Display current date
            date
            echo ""
            echo "Type 'help' for commands or 'man <topic>' for detailed help."
            echo ""
            """;

        const string skelDemo = """
            #!/bin/nsh
            # demo.sh — A sample shell script demonstrating nsh features
            # Run with:  ./demo.sh   or   source demo.sh

            echo "=== NetNIX Shell Script Demo ==="
            echo ""

            # ?? Variables ??????????????????????????????????
            # The shell expands these before each line runs:
            echo "Hello, $USER! Your UID is $UID, GID is $GID."
            echo "Home directory: $HOME"
            echo "Current directory: $PWD"
            echo "Shell: $SHELL on $HOSTNAME"
            echo ""

            # ?? Running commands ???????????????????????????
            echo "--- System info ---"
            uname -a
            echo ""

            echo "--- Current date ---"
            date
            echo ""

            echo "--- Who am I? ---"
            whoami
            id
            echo ""

            # ?? Working with files ?????????????????????????
            echo "--- Creating a temp file ---"
            echo "This file was created by demo.sh" > /tmp/demo_output.txt
            echo "Written to /tmp/demo_output.txt"
            cat /tmp/demo_output.txt
            echo ""

            echo "--- Appending to the file ---"
            echo "User: $USER, Date: " >> /tmp/demo_output.txt
            date >> /tmp/demo_output.txt
            cat /tmp/demo_output.txt
            echo ""

            # ?? Listing directories ????????????????????????
            echo "--- Your home directory ---"
            ls $HOME
            echo ""

            echo "--- Commands in /bin (first 10) ---"
            ls /bin
            echo ""

            # ?? Searching ??????????????????????????????????
            echo "--- Finding .sh files ---"
            find $HOME -name *.sh -type f
            echo ""

            # ?? Cleanup ????????????????????????????????????
            echo "--- Cleaning up ---"
            rm /tmp/demo_output.txt
            echo "Removed /tmp/demo_output.txt"
            echo ""

            echo "=== Demo complete! ==="
            echo "Edit this script with: edit ~/demo.sh"
            echo "See 'man nshrc' and 'man source' for more info."
            echo ""
            """;

        var skelFiles = new Dictionary<string, string>
        {
            ["/etc/skel/.nshrc"]   = skelNshrc,
            ["/etc/skel/demo.sh"]  = skelDemo,
        };

        foreach (var (path, content) in skelFiles)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(content);
            if (fs.IsFile(path))
                fs.WriteFile(path, data);
            else
                fs.CreateFile(path, 0, 0, data, "rw-r--r--");
        }
    }

    private static void InstallUserFiles(VirtualFileSystem fs, string username, int uid, int gid, string home)
    {
        string nshrcPath = home.TrimEnd('/') + "/.nshrc";
        string nshrcContent = BuildNshrc(username, uid);
        var nshrcData = System.Text.Encoding.UTF8.GetBytes(nshrcContent);
        if (fs.IsFile(nshrcPath))
            fs.WriteFile(nshrcPath, nshrcData);
        else
            fs.CreateFile(nshrcPath, uid, gid, nshrcData, "rw-r--r--");

        // Copy demo.sh from /etc/skel if it exists
        const string skelDemo = "/etc/skel/demo.sh";
        string demoPath = home.TrimEnd('/') + "/demo.sh";
        if (fs.IsFile(skelDemo) && !fs.IsFile(demoPath))
        {
            byte[] demoData = fs.ReadFile(skelDemo);
            fs.CreateFile(demoPath, uid, gid, demoData, "rw-r--r--");
        }
    }

    public static void InstallBuiltinScripts(VirtualFileSystem fs)
    {
        var scripts = BuiltinScripts.LoadAll();

        if (scripts.Count == 0)
        {
            Console.WriteLine("  Warning: No builtin scripts found to install.");
            return;
        }

        foreach (var (path, source) in scripts)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(source);
            if (fs.IsFile(path))
                fs.WriteFile(path, data);
            else
                fs.CreateFile(path, 0, 0, data, "rwxr-xr-x");
        }

        int binCount = scripts.Count(kv => kv.Key.StartsWith("/bin/"));
        int sbinCount = scripts.Count(kv => kv.Key.StartsWith("/sbin/"));
        Console.WriteLine($"  Installed {binCount} commands in /bin/, {sbinCount} in /sbin/");
    }

    public static void InstallBuiltinLibs(VirtualFileSystem fs)
    {
        var libs = BuiltinLibs.LoadAll();

        if (libs.Count == 0)
        {
            Console.WriteLine("  No built-in libraries found.");
            return;
        }

        foreach (var (path, source) in libs)
        {
            var data = System.Text.Encoding.UTF8.GetBytes(source);
            if (fs.IsFile(path))
                fs.WriteFile(path, data);
            else
                fs.CreateFile(path, 0, 0, data, "rw-r--r--");
        }

        Console.WriteLine($"  Installed {libs.Count} libraries in /lib/");
    }

    public static void InstallManPages(VirtualFileSystem fs)
    {
        var pages = BuiltinManPages.LoadAll();

        if (pages.Count == 0)
        {
            Console.WriteLine("  Warning: No man pages found to install.");
            return;
        }

        foreach (var (name, content) in pages)
        {
            string path = $"/usr/share/man/{name}.txt";
            var data = System.Text.Encoding.UTF8.GetBytes(content);
            if (fs.IsFile(path))
                fs.WriteFile(path, data);
            else
                fs.CreateFile(path, 0, 0, data, "rw-r--r--");
        }

        Console.WriteLine($"  Installed {pages.Count} man pages in /usr/share/man/");
    }

    public static void InstallFactoryFiles(VirtualFileSystem fs)
    {
        // Ensure all required directories exist first
        var dirs = FactoryFiles.GetDirectories();
        foreach (var dir in dirs)
        {
            if (!fs.Exists(dir))
                fs.CreateDirectory(dir, 0, 0, "rwxr-xr-x");
        }

        var files = FactoryFiles.LoadAll();

        if (files.Count == 0)
        {
            Console.WriteLine("  No factory files found.");
            return;
        }

        foreach (var (path, data) in files)
        {
            // Ensure parent directories exist (in case they aren't
            // covered by GetDirectories, e.g. deeply nested paths)
            EnsureParentDirs(fs, path);

            if (fs.IsFile(path))
                fs.WriteFile(path, data);
            else
                fs.CreateFile(path, 0, 0, data, "rw-r--r--");
        }

        Console.WriteLine($"  Installed {files.Count} factory file(s)");
    }

    private static void EnsureParentDirs(VirtualFileSystem fs, string path)
    {
        // Walk up from the file path and create any missing directories
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";
        for (int i = 0; i < parts.Length - 1; i++) // skip the filename
        {
            current += "/" + parts[i];
            if (!fs.Exists(current))
                fs.CreateDirectory(current, 0, 0, "rwxr-xr-x");
        }
    }

    private static string? ReadPassword()
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
}
