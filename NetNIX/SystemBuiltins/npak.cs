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
    private const string ReposDir = "/etc/npak";
    private const string ReposFile = "/etc/npak/repos.conf";

    private const string DefaultRepos = """
        # /etc/npak/repos.conf — npak repository list
        # Format: type=url
        #
        # Supported types:
        #   HTTPFile  — directory listing; scans HTML <a> links for .npak files
        #
        # Add or remove repos as needed. Lines starting with # are comments.
        HTTPFile=http://npak.controlfeed.org/
        """;

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
            "get" => Get(api, subArgs),
            _ => UnknownSubcommand(subcommand),
        };
    }

    // ?? install ????????????????????????????????????????????????????

    private static int Install(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("npak: install requires at least one package path");
            Console.WriteLine("Usage: npak install <package.npak> [package2.npak ...]");
            return 1;
        }

        if (api.Uid != 0)
        {
            Console.WriteLine("npak: permission denied (must be root)");
            return 1;
        }

        int failures = 0;
        foreach (string pkgPath in args)
        {
            if (args.Length > 1)
                Console.WriteLine($"\n--- {pkgPath} ---");

            int result = InstallSingle(api, pkgPath);
            if (result != 0)
                failures++;
        }

        if (args.Length > 1)
        {
            int ok = args.Length - failures;
            Console.WriteLine($"\nnpak: {ok} of {args.Length} package(s) installed successfully");
        }

        return failures > 0 ? 1 : 0;
    }

    private static int InstallSingle(NixApi api, string pkgPath)
    {
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
        string pkgDeps = manifest.GetValueOrDefault("deps", "").Trim();

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
        if (!string.IsNullOrEmpty(pkgDeps))
            receipt += $"deps={pkgDeps}\n";
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
        string deps = manifest.GetValueOrDefault("deps", "").Trim();
        if (!string.IsNullOrEmpty(deps))
            Console.WriteLine($"Depends on:  {deps}");
        Console.WriteLine($"Files:       {files.Count}");
        foreach (var f in files)
            Console.WriteLine($"  {f}");

        return 0;
    }

    // ?? get — remote package operations ??????????????????????????????????

    private static int Get(NixApi api, string[] args)
    {
        if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
        {
            PrintGetUsage();
            return args.Length == 0 ? 1 : 0;
        }

        // Ensure repos file exists
        EnsureReposFile(api);

        string action = args[0];
        string[] actionArgs = args.Skip(1).ToArray();

        return action switch
        {
            "install" => GetInstall(api, actionArgs),
            "search" => GetSearch(api, actionArgs),
            "repos" => GetRepos(api),
            _ => GetUnknown(action),
        };
    }

    private static int GetInstall(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("npak: get install requires at least one package name");
            Console.WriteLine("Usage: npak get install <name> [name2 ...]");
            return 1;
        }

        if (api.Uid != 0)
        {
            Console.WriteLine("npak: permission denied (must be root)");
            return 1;
        }

        var repos = LoadRepos(api);
        if (repos.Count == 0)
        {
            Console.WriteLine("npak: no repositories configured. Edit " + ReposFile);
            return 1;
        }

        int failures = 0;
        foreach (string pkgName in args)
        {
            if (args.Length > 1)
                Console.WriteLine($"\n--- {pkgName} ---");

            int result = GetInstallSingle(api, pkgName, repos);
            if (result != 0)
                failures++;
        }

        if (args.Length > 1)
        {
            int ok = args.Length - failures;
            Console.WriteLine($"\nnpak: {ok} of {args.Length} remote package(s) installed successfully");
        }

        return failures > 0 ? 1 : 0;
    }

    private static int GetInstallSingle(NixApi api, string pkgName, List<(string type, string url)> repos)
    {
        // Check if already installed
        string receiptPath = DbDir + "/" + pkgName + ".list";
        if (api.IsFile(receiptPath))
        {
            Console.WriteLine($"npak: '{pkgName}' is already installed. Use 'npak remove {pkgName}' first.");
            return 1;
        }

        Console.WriteLine($"Searching for '{pkgName}'...");

        foreach (var (type, url) in repos)
        {
            if (!type.Equals("HTTPFile", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  skipping unknown repo type '{type}'");
                continue;
            }

            string downloadUrl = SearchHTTPFileRepo(api, url, pkgName);
            if (downloadUrl == null)
                continue;

            Console.WriteLine($"  found: {downloadUrl}");
            Console.WriteLine($"  downloading...");

            byte[] data = api.Net.GetBytes(downloadUrl);
            if (data == null)
            {
                Console.WriteLine($"npak: failed to download {downloadUrl}");
                continue;
            }

            // Write to /tmp and install
            string tmpPath = "/tmp/" + pkgName + ".npak";
            api.WriteBytes(tmpPath, data);
            Console.WriteLine($"  downloaded {data.Length} bytes -> {tmpPath}");

            int result = InstallSingle(api, tmpPath);

            // Cleanup temp file
            if (api.IsFile(tmpPath))
                api.Delete(tmpPath);

            // Install dependencies if the package declared any
            if (result == 0)
            {
                if (api.IsFile(receiptPath))
                {
                    var manifest = ParseManifest(api.ReadText(receiptPath));
                    string deps = manifest.GetValueOrDefault("deps", "").Trim();
                    if (!string.IsNullOrEmpty(deps))
                    {
                        string[] depNames = deps.Split(',');
                        foreach (string rawDep in depNames)
                        {
                            string dep = rawDep.Trim();
                            if (dep.Length == 0) continue;

                            string depReceipt = DbDir + "/" + dep + ".list";
                            if (api.IsFile(depReceipt))
                            {
                                Console.WriteLine($"  dependency '{dep}' is already installed");
                                continue;
                            }

                            Console.WriteLine($"  installing dependency '{dep}'...");
                            int depResult = GetInstallSingle(api, dep, repos);
                            if (depResult != 0)
                                Console.WriteLine($"npak: warning: failed to install dependency '{dep}'");
                        }
                    }
                }
            }

            return result;
        }

        Console.WriteLine($"npak: '{pkgName}' not found in any repository");
        return 1;
    }

    // ?? search ??????????????????????????????????????????????????????

    private static int GetSearch(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("npak: get search requires a search term");
            Console.WriteLine("Usage: npak get search <query>");
            return 1;
        }

        string query = args[0];
        var repos = LoadRepos(api);
        if (repos.Count == 0)
        {
            Console.WriteLine("npak: no repositories configured. Edit " + ReposFile);
            return 1;
        }

        bool anyFound = false;
        foreach (var (type, url) in repos)
        {
            if (!type.Equals("HTTPFile", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  skipping unknown repo type '{type}'");
                continue;
            }

            Console.WriteLine($"[{url}]");
            var packages = ListHTTPFilePackages(api, url);
            if (packages == null)
            {
                Console.WriteLine("  (failed to fetch repository listing)");
                continue;
            }

            bool repoHit = false;
            foreach (var pkg in packages)
            {
                if (pkg.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine($"  {pkg}");
                    repoHit = true;
                    anyFound = true;
                }
            }

            if (!repoHit)
                Console.WriteLine("  (no matches)");
        }

        if (!anyFound)
            Console.WriteLine($"npak: no packages matching '{query}' found in any repository");

        return anyFound ? 0 : 1;
    }

    // ?? HTTPFile repo helpers ????????????????????????????????????????????

    /// <summary>
    /// Fetch the HTML directory listing at the given URL and return all
    /// .npak package names (without extension). Returns null on failure.
    /// </summary>
    private static List<string> ListHTTPFilePackages(NixApi api, string baseUrl)
    {
        string html = api.Net.Get(baseUrl);
        if (html == null)
            return null;

        var packages = new List<string>();
        int pos = 0;
        while (pos < html.Length)
        {
            int hrefIdx = html.IndexOf("href=", pos, StringComparison.OrdinalIgnoreCase);
            if (hrefIdx < 0)
                break;

            int quotePos = hrefIdx + 5;
            if (quotePos >= html.Length)
                break;

            char quote = html[quotePos];
            if (quote != '"' && quote != '\'')
            {
                pos = quotePos + 1;
                continue;
            }

            int start = quotePos + 1;
            int end = html.IndexOf(quote, start);
            if (end < 0)
                break;

            string href = html.Substring(start, end - start);
            string linkName = href;
            int lastSlash = linkName.LastIndexOf('/');
            if (lastSlash >= 0)
                linkName = linkName[(lastSlash + 1)..];

            if (linkName.EndsWith(".npak", StringComparison.OrdinalIgnoreCase))
            {
                string name = linkName[..^5]; // strip .npak
                packages.Add(name);
            }

            pos = end + 1;
        }

        return packages;
    }

    /// <summary>
    /// For an HTTPFile repo, search for an exact package name and return
    /// the full download URL, or null if not found.
    /// </summary>
    private static string SearchHTTPFileRepo(NixApi api, string baseUrl, string pkgName)
    {
        var packages = ListHTTPFilePackages(api, baseUrl);
        if (packages == null)
            return null;

        // Check for exact match
        if (!packages.Any(p => p.Equals(pkgName, StringComparison.OrdinalIgnoreCase)))
            return null;

        string normalizedBase = baseUrl.TrimEnd('/') + "/";
        return normalizedBase + pkgName + ".npak";
    }

    private static int GetRepos(NixApi api)
    {
        EnsureReposFile(api);
        var repos = LoadRepos(api);

        if (repos.Count == 0)
        {
            Console.WriteLine("No repositories configured.");
            Console.WriteLine("Edit " + ReposFile + " to add repositories.");
            return 0;
        }

        Console.WriteLine("Configured repositories:");
        foreach (var (type, url) in repos)
            Console.WriteLine($"  [{type}] {url}");

        return 0;
    }

    // ?? repo helpers ??????????????????????????????????????????????????

    private static void EnsureReposFile(NixApi api)
    {
        if (api.IsFile(ReposFile))
            return;

        if (!api.IsDirectory(ReposDir))
            api.CreateDirWithParents(ReposDir);

        api.WriteText(ReposFile, DefaultRepos);
        Console.WriteLine("npak: created default repository list at " + ReposFile);
        api.Save();
    }

    private static List<(string type, string url)> LoadRepos(NixApi api)
    {
        var repos = new List<(string type, string url)>();

        if (!api.IsFile(ReposFile))
            return repos;

        string content = api.ReadText(ReposFile);
        foreach (var rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string type = line[..eq].Trim();
            string url = line[(eq + 1)..].Trim();

            if (type.Length > 0 && url.Length > 0)
                repos.Add((type, url));
        }

        return repos;
    }

    private static int GetUnknown(string action)
    {
        Console.WriteLine($"npak: get: unknown action '{action}'");
        Console.WriteLine("Usage: npak get search <query>  |  npak get install <name>  |  npak get repos");
        return 1;
    }

    private static void PrintGetUsage()
    {
        Console.WriteLine("npak get — remote package operations");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  npak get search <query>        Search repositories for packages");
        Console.WriteLine("  npak get install <name> [...]   Download and install from repositories");
        Console.WriteLine("  npak get repos                  List configured repositories");
        Console.WriteLine();
        Console.WriteLine("Repository configuration: " + ReposFile);
        Console.WriteLine();
        Console.WriteLine("Supported repo types:");
        Console.WriteLine("  HTTPFile  Scans HTML directory listing for .npak file links");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  npak get search kobold");
        Console.WriteLine("  npak get search lib");
        Console.WriteLine("  npak get install demo");
        Console.WriteLine("  npak get install dotnet-lib-emu myapp");
        Console.WriteLine("  npak get repos");
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
        Console.WriteLine("  npak install <pkg.npak> [...]   Install one or more local packages");
        Console.WriteLine("  npak remove <name>              Remove an installed package");
        Console.WriteLine("  npak list                       List installed packages");
        Console.WriteLine("  npak info <name>                Show package details");
        Console.WriteLine("  npak get search <query>         Search repositories for packages");
        Console.WriteLine("  npak get install <name> [...]    Download and install from repositories");
        Console.WriteLine("  npak get repos                   List configured repositories");
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
        Console.WriteLine("  npak install /tmp/foo.npak /tmp/bar.npak");
        Console.WriteLine("  npak get search kobold");
        Console.WriteLine("  npak get install demo");
        Console.WriteLine("  npak get install dotnet-lib-emu myapp");
        Console.WriteLine("  npak get repos");
        Console.WriteLine("  npak list");
        Console.WriteLine("  npak info myapp");
        Console.WriteLine("  npak remove myapp");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Only root can install or remove packages.");
        Console.WriteLine("  Installed files go to /usr/local/{bin,lib} and /usr/share/man.");
        Console.WriteLine("  Package receipts are stored in /var/lib/npak/.");
        Console.WriteLine("  Repository list: /etc/npak/repos.conf");
    }
}
