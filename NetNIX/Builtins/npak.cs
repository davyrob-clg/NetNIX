using System;
using System.Collections.Generic;
using System.Linq;
using NetNIX.Scripting;

/// <summary>
/// npak — NetNIX package manager.
///
/// Packages are zip files with the .npak extension containing:
///   manifest.txt   — package metadata (name, version, description, type)
///   bin/           — executable scripts installed to /usr/local/bin/
///   lib/           — library files installed to /usr/local/lib/
///   man/           — manual pages installed to /usr/share/man/
///
/// Installed package receipts are stored in /var/lib/npak/
/// </summary>
public static class NpakCommand
{
    private const string DbDir = "/var/lib/npak";

    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        string subcommand = args[0];
        string[] subArgs = args.Skip(1).ToArray();

        return subcommand switch
        {
            "install" => Install(api, subArgs),
            "remove" => Remove(api, subArgs),
            "list" => ListInstalled(api),
            "info" => Info(api, subArgs),
            _ => UnknownSubcommand(subcommand),
        };
    }

    // ?? install ????????????????????????????????????????????????????

    private static int Install(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("npak: install requires a package path");
            Console.WriteLine("Usage: npak install <package.npak>");
            return 1;
        }

        if (api.Uid != 0)
        {
            Console.WriteLine("npak: permission denied (must be root)");
            return 1;
        }

        string pkgPath = args[0];
        if (!api.IsFile(pkgPath))
        {
            Console.WriteLine($"npak: {pkgPath}: not found");
            return 1;
        }

        // List contents of the .npak zip
        var entries = api.ZipList(pkgPath);
        if (entries == null)
        {
            Console.WriteLine("npak: failed to read package");
            return 1;
        }

        // Check for manifest
        bool hasManifest = entries.Any(e => e.name.Equals("manifest.txt", StringComparison.OrdinalIgnoreCase));
        if (!hasManifest)
        {
            Console.WriteLine("npak: invalid package — missing manifest.txt");
            return 1;
        }

        // Extract to a temporary staging directory
        string staging = "/tmp/.npak-staging";
        if (api.IsDirectory(staging))
            api.Delete(staging);
        api.CreateDirWithParents(staging);

        int extracted = api.ZipExtract(pkgPath, staging);
        if (extracted < 0)
        {
            Console.WriteLine("npak: failed to extract package");
            return 1;
        }

        // Read manifest
        string manifestPath = staging + "/manifest.txt";
        if (!api.IsFile(manifestPath))
        {
            Console.WriteLine("npak: invalid package — manifest.txt not found after extraction");
            Cleanup(api, staging);
            return 1;
        }

        var manifest = ParseManifest(api.ReadText(manifestPath));
        string pkgName = manifest.GetValueOrDefault("name", "").Trim();
        string pkgVersion = manifest.GetValueOrDefault("version", "unknown").Trim();
        string pkgDesc = manifest.GetValueOrDefault("description", "").Trim();
        string pkgType = manifest.GetValueOrDefault("type", "app").Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(pkgName))
        {
            Console.WriteLine("npak: invalid manifest — missing 'name' field");
            Cleanup(api, staging);
            return 1;
        }

        // Check if already installed
        string receiptPath = DbDir + "/" + pkgName + ".list";
        if (api.IsFile(receiptPath))
        {
            Console.WriteLine($"npak: '{pkgName}' is already installed. Use 'npak remove {pkgName}' first.");
            Cleanup(api, staging);
            return 1;
        }

        Console.WriteLine($"Installing {pkgName} {pkgVersion}...");
        var installedFiles = new List<string>();

        // Install bin/ -> /usr/local/bin/
        installedFiles.AddRange(InstallDir(api, staging + "/bin", "/usr/local/bin", "rwxr-xr-x"));

        // Install lib/ -> /usr/local/lib/
        installedFiles.AddRange(InstallDir(api, staging + "/lib", "/usr/local/lib", "rw-r--r--"));

        // Install man/ -> /usr/share/man/
        installedFiles.AddRange(InstallDir(api, staging + "/man", "/usr/share/man", "rw-r--r--"));

        // Write install receipt
        string receipt = $"name={pkgName}\nversion={pkgVersion}\ndescription={pkgDesc}\ntype={pkgType}\n";
        receipt += "[files]\n";
        foreach (var f in installedFiles)
            receipt += f + "\n";

        if (!api.IsDirectory(DbDir))
            api.CreateDirWithParents(DbDir);
        api.WriteText(receiptPath, receipt);

        // Cleanup staging
        Cleanup(api, staging);

        Console.WriteLine($"  {installedFiles.Count} file(s) installed");
        Console.WriteLine($"npak: {pkgName} {pkgVersion} installed successfully");
        api.Save();
        return 0;
    }

    /// <summary>
    /// Copy files from a staging subdirectory to a system target directory.
    /// Returns the list of installed VFS paths.
    /// </summary>
    private static List<string> InstallDir(NixApi api, string srcDir, string destDir, string permissions)
    {
        var installed = new List<string>();
        if (!api.IsDirectory(srcDir))
            return installed;

        var files = api.ListDirectory(srcDir);
        foreach (var filePath in files)
        {
            string name = api.NodeName(filePath);
            // Skip subdirectories — only install files
            if (api.IsDirectory(filePath))
                continue;

            string dest = destDir + "/" + name;
            byte[] data = api.ReadBytes(filePath);

            if (api.IsFile(dest))
            {
                api.WriteBytes(dest, data);
            }
            else
            {
                api.WriteBytes(dest, data);
                // WriteBytes creates the file if it doesn't exist via the API,
                // but we need to ensure correct permissions — re-create if needed
            }

            Console.WriteLine($"  {dest}");
            installed.Add(dest);
        }

        return installed;
    }

    // ?? remove ?????????????????????????????????????????????????????

    private static int Remove(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("npak: remove requires a package name");
            Console.WriteLine("Usage: npak remove <name>");
            return 1;
        }

        if (api.Uid != 0)
        {
            Console.WriteLine("npak: permission denied (must be root)");
            return 1;
        }

        string pkgName = args[0];
        string receiptPath = DbDir + "/" + pkgName + ".list";

        if (!api.IsFile(receiptPath))
        {
            Console.WriteLine($"npak: '{pkgName}' is not installed");
            return 1;
        }

        string receipt = api.ReadText(receiptPath);
        var files = ParseInstalledFiles(receipt);

        Console.WriteLine($"Removing {pkgName}...");
        int removed = 0;
        foreach (var f in files)
        {
            if (api.IsFile(f))
            {
                api.Delete(f);
                Console.WriteLine($"  removed {f}");
                removed++;
            }
        }

        // Remove the receipt
        api.Delete(receiptPath);

        Console.WriteLine($"npak: {pkgName} removed ({removed} file(s))");
        api.Save();
        return 0;
    }

    // ?? list ????????????????????????????????????????????????????????

    private static int ListInstalled(NixApi api)
    {
        if (!api.IsDirectory(DbDir))
        {
            Console.WriteLine("No packages installed.");
            return 0;
        }

        var entries = api.ListDirectory(DbDir);
        bool any = false;

        foreach (var path in entries)
        {
            string name = api.NodeName(path);
            if (!name.EndsWith(".list")) continue;

            string receipt = api.ReadText(path);
            var manifest = ParseManifest(receipt);
            string pkgName = manifest.GetValueOrDefault("name", name.Replace(".list", ""));
            string pkgVersion = manifest.GetValueOrDefault("version", "?");
            string pkgDesc = manifest.GetValueOrDefault("description", "");

            if (pkgDesc.Length > 0)
                Console.WriteLine($"  {pkgName} {pkgVersion} — {pkgDesc}");
            else
                Console.WriteLine($"  {pkgName} {pkgVersion}");
            any = true;
        }

        if (!any)
            Console.WriteLine("No packages installed.");

        return 0;
    }

    // ?? info ?????????????????????????????????????????????????????????

    private static int Info(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("npak: info requires a package name");
            return 1;
        }

        string pkgName = args[0];
        string receiptPath = DbDir + "/" + pkgName + ".list";

        if (!api.IsFile(receiptPath))
        {
            Console.WriteLine($"npak: '{pkgName}' is not installed");
            return 1;
        }

        string receipt = api.ReadText(receiptPath);
        var manifest = ParseManifest(receipt);
        var files = ParseInstalledFiles(receipt);

        Console.WriteLine($"Name:        {manifest.GetValueOrDefault("name", pkgName)}");
        Console.WriteLine($"Version:     {manifest.GetValueOrDefault("version", "?")}");
        Console.WriteLine($"Type:        {manifest.GetValueOrDefault("type", "?")}");
        Console.WriteLine($"Description: {manifest.GetValueOrDefault("description", "")}");
        Console.WriteLine($"Files:       {files.Count}");
        foreach (var f in files)
            Console.WriteLine($"  {f}");

        return 0;
    }

    // ?? helpers ??????????????????????????????????????????????????????

    private static Dictionary<string, string> ParseManifest(string text)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('['))
                continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq].Trim();
            string val = line[(eq + 1)..].Trim();
            dict[key] = val;
        }
        return dict;
    }

    private static List<string> ParseInstalledFiles(string receipt)
    {
        var files = new List<string>();
        bool inFiles = false;
        foreach (var rawLine in receipt.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line == "[files]") { inFiles = true; continue; }
            if (line.StartsWith('[')) { inFiles = false; continue; }
            if (inFiles && line.Length > 0)
                files.Add(line);
        }
        return files;
    }

    private static void Cleanup(NixApi api, string dir)
    {
        try { api.Delete(dir); } catch { /* best effort */ }
    }

    private static int UnknownSubcommand(string cmd)
    {
        Console.WriteLine($"npak: unknown subcommand '{cmd}'");
        Console.WriteLine("Run 'npak --help' for usage.");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("npak — NetNIX package manager");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  npak install <package.npak>    Install a package");
        Console.WriteLine("  npak remove <name>             Remove an installed package");
        Console.WriteLine("  npak list                      List installed packages");
        Console.WriteLine("  npak info <name>               Show package details");
        Console.WriteLine();
        Console.WriteLine("Package format (.npak):");
        Console.WriteLine("  A zip file containing:");
        Console.WriteLine("    manifest.txt   name, version, description, type");
        Console.WriteLine("    bin/           Scripts installed to /usr/local/bin/");
        Console.WriteLine("    lib/           Libraries installed to /usr/local/lib/");
        Console.WriteLine("    man/           Man pages installed to /usr/share/man/");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help    Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  npak install /tmp/myapp.npak");
        Console.WriteLine("  npak list");
        Console.WriteLine("  npak info myapp");
        Console.WriteLine("  npak remove myapp");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Only root can install or remove packages.");
        Console.WriteLine("  Installed files go to /usr/local/{bin,lib} and /usr/share/man.");
        Console.WriteLine("  Package receipts are stored in /var/lib/npak/.");
    }
}
