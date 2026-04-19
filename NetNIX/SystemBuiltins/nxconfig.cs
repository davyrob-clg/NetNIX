using System;
using System.Collections.Generic;
using System.Linq;
using NetNIX.Scripting;

/// <summary>
/// nxconfig — View and manage NetNIX host environment configuration.
///
/// Usage:
///     nxconfig                Show all settings
///     nxconfig get key        Get a setting value
///     nxconfig set key value  Change a setting (root only)
///     nxconfig path           Show config file path
///     nxconfig --help         Show help
/// </summary>
public static class NxConfigCommand
{
    // The config file lives next to the executable, not in the VFS.
    // We read/write it directly via the host filesystem.
    private static readonly string ConfigPath = System.IO.Path.Combine(
        AppContext.BaseDirectory, "config", "netnix.conf");

    public static int Run(NixApi api, string[] args)
    {
        var argList = new List<string>(args);

        if (argList.Contains("-h") || argList.Contains("--help"))
        {
            PrintUsage();
            return 0;
        }

        if (argList.Count == 0)
            return CmdShow();

        string sub = argList[0].ToLowerInvariant();

        switch (sub)
        {
            case "path":
                Console.WriteLine(ConfigPath);
                return 0;

            case "get":
                if (argList.Count < 2)
                {
                    Console.WriteLine("nxconfig: get requires a key");
                    return 1;
                }
                return CmdGet(argList[1]);

            case "set":
                if (argList.Count < 3)
                {
                    Console.WriteLine("nxconfig: set requires a key and value");
                    return 1;
                }
                if (api.Uid != 0)
                {
                    Console.WriteLine("nxconfig: only root can change environment settings");
                    return 1;
                }
                return CmdSet(argList[1], string.Join(" ", argList.Skip(2)));

            default:
                Console.WriteLine($"nxconfig: unknown subcommand '{sub}'");
                Console.WriteLine("Run 'nxconfig --help' for usage.");
                return 1;
        }
    }

    private static int CmdShow()
    {
        if (!System.IO.File.Exists(ConfigPath))
        {
            Console.WriteLine("nxconfig: config file not found at " + ConfigPath);
            return 1;
        }

        var settings = LoadConfig();

        Console.WriteLine("NetNIX Environment Configuration");
        Console.WriteLine("  Config file: " + ConfigPath);
        Console.WriteLine();

        // Known keys with descriptions
        var descriptions = new (string key, string desc)[]
        {
            ("allow_reset",      "Allow Ctrl+R reset and __reset__ login"),
            ("rootfs_path",      "Custom rootfs archive path (empty = default)"),
            ("instance_name",    "Instance name for this environment"),
            ("show_boot_banner", "Show boot banner on startup"),
            ("boot_timeout",     "Seconds to wait for Ctrl+R at boot"),
            ("login_banner",     "Text shown at login prompt"),
            ("motd_enabled",     "Show /etc/motd after boot"),
            ("auto_save",        "Auto-save VFS on logout"),
        };

        Console.WriteLine("  Key                Value                          Description");
        Console.WriteLine("  ?????????????????  ?????????????????????????????  ?????????????????????????????????????????");

        foreach (var (key, desc) in descriptions)
        {
            string val = settings.GetValueOrDefault(key, "(not set)");
            Console.WriteLine($"  {key,-17}  {val,-30} {desc}");
        }

        // Show any extra keys not in the known list
        var knownKeys = new HashSet<string>(descriptions.Select(d => d.key), StringComparer.OrdinalIgnoreCase);
        foreach (var kv in settings)
        {
            if (!knownKeys.Contains(kv.Key))
                Console.WriteLine($"  {kv.Key,-17}  {kv.Value,-30} (custom)");
        }

        Console.WriteLine();
        Console.WriteLine("Changes take effect on next boot (except instance_name, login_banner).");
        Console.WriteLine("Use 'sudo nxconfig set <key> <value>' to change a setting.");

        return 0;
    }

    private static int CmdGet(string key)
    {
        var settings = LoadConfig();
        if (settings.TryGetValue(key, out var val))
        {
            Console.WriteLine(val);
            return 0;
        }
        Console.WriteLine($"nxconfig: '{key}' is not set");
        return 1;
    }

    private static int CmdSet(string key, string value)
    {
        var settings = LoadConfig();
        settings[key] = value;
        SaveConfig(settings);
        Console.WriteLine($"nxconfig: {key} = {value}");
        Console.WriteLine("Changes take effect on next boot.");
        return 0;
    }

    private static Dictionary<string, string> LoadConfig()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!System.IO.File.Exists(ConfigPath))
            return dict;

        foreach (var rawLine in System.IO.File.ReadAllLines(ConfigPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            dict[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        return dict;
    }

    private static void SaveConfig(Dictionary<string, string> settings)
    {
        // Preserve comments, update values in-place
        var lines = new List<string>();
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (System.IO.File.Exists(ConfigPath))
        {
            foreach (var rawLine in System.IO.File.ReadAllLines(ConfigPath))
            {
                string trimmed = rawLine.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    lines.Add(rawLine);
                    continue;
                }

                int eq = trimmed.IndexOf('=');
                if (eq > 0)
                {
                    string key = trimmed.Substring(0, eq).Trim();
                    if (settings.TryGetValue(key, out var val))
                    {
                        lines.Add($"{key}={val}");
                        written.Add(key);
                        continue;
                    }
                }
                lines.Add(rawLine);
            }
        }

        foreach (var kv in settings)
        {
            if (!written.Contains(kv.Key))
                lines.Add($"{kv.Key}={kv.Value}");
        }

        System.IO.File.WriteAllLines(ConfigPath, lines);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("nxconfig — NetNIX environment configuration");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  nxconfig                  Show all settings");
        Console.WriteLine("  nxconfig get <key>        Get a setting value");
        Console.WriteLine("  nxconfig set <key> <val>  Change a setting (root only)");
        Console.WriteLine("  nxconfig path             Show config file path");
        Console.WriteLine("  nxconfig --help           Show this help");
        Console.WriteLine();
        Console.WriteLine("Settings:");
        Console.WriteLine("  allow_reset        Enable/disable Ctrl+R and __reset__ (true/false)");
        Console.WriteLine("  rootfs_path        Custom rootfs archive path (empty = default)");
        Console.WriteLine("  instance_name      Name for this NetNIX instance");
        Console.WriteLine("  show_boot_banner   Show boot banner (true/false)");
        Console.WriteLine("  boot_timeout       Seconds to wait for Ctrl+R (0 = skip)");
        Console.WriteLine("  login_banner       Text shown at login prompt");
        Console.WriteLine("  motd_enabled       Show /etc/motd on boot (true/false)");
        Console.WriteLine("  auto_save          Save VFS on logout (true/false)");
        Console.WriteLine();
        Console.WriteLine("The config file is stored on the host filesystem at:");
        Console.WriteLine("  <exe-dir>/config/netnix.conf");
        Console.WriteLine();
        Console.WriteLine("Changes take effect on next boot.");
        Console.WriteLine();
        Console.WriteLine("See also: man nxconfig");
    }
}
