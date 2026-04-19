namespace NetNIX.Config;

/// <summary>
/// Manages NetNIX host-side environment configuration.
///
/// Settings are stored in a config/netnix.conf file alongside
/// the NetNIX executable. This file is NOT inside the VFS — it
/// controls how the environment itself boots and behaves.
///
/// Format: simple key=value lines. Lines starting with # are comments.
/// </summary>
public sealed class NxConfig
{
    private readonly string _configPath;
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Default configuration values.
    /// </summary>
    public static readonly Dictionary<string, string> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["allow_reset"]       = "true",
        ["rootfs_path"]       = "",
        ["instance_name"]     = "NetNIX Machine",
        ["show_boot_banner"]  = "true",
        ["boot_timeout"]      = "3",
        ["motd_enabled"]      = "true",
        ["auto_save"]         = "true",
        ["login_banner"]      = "NetNIX Login",
    };

    // --- Typed property accessors ---

    /// <summary>
    /// Whether the Ctrl+R reset at boot and the __reset__ login are enabled.
    /// Default: true
    /// </summary>
    public bool AllowReset => GetBool("allow_reset", true);

    /// <summary>
    /// Custom path to the rootfs archive. If empty, uses the default
    /// AppData/Roaming/NetNIX/rootfs.zip location.
    /// Can be an absolute path or a path relative to the executable directory.
    /// </summary>
    public string RootfsPath => Get("rootfs_path", "");

    /// <summary>
    /// A human-readable name for this NetNIX instance.
    /// Used in boot banners and available to scripts for future networking.
    /// Default: "NetNIX Machine"
    /// </summary>
    public string InstanceName => Get("instance_name", "NetNIX Machine");

    /// <summary>
    /// Whether to show the boot banner on startup.
    /// Default: true
    /// </summary>
    public bool ShowBootBanner => GetBool("show_boot_banner", true);

    /// <summary>
    /// Seconds to wait for Ctrl+R at boot (only relevant if AllowReset is true).
    /// Default: 3
    /// </summary>
    public int BootTimeout => GetInt("boot_timeout", 3);

    /// <summary>
    /// Whether to display /etc/motd after boot.
    /// Default: true
    /// </summary>
    public bool MotdEnabled => GetBool("motd_enabled", true);

    /// <summary>
    /// Whether to auto-save the VFS on logout.
    /// Default: true
    /// </summary>
    public bool AutoSave => GetBool("auto_save", true);

    /// <summary>
    /// Custom text shown at the login prompt.
    /// Default: "NetNIX Login"
    /// </summary>
    public string LoginBanner => Get("login_banner", "NetNIX Login");

    // --- Constructor ---

    public NxConfig()
    {
        string configDir = Path.Combine(AppContext.BaseDirectory, "config");
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "netnix.conf");

        if (!File.Exists(_configPath))
            CreateDefault();

        Load();
    }

    // --- Public API ---

    public string Get(string key, string defaultValue = "")
    {
        return _values.TryGetValue(key, out var val) ? val : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (_values.TryGetValue(key, out var val))
            return val.Equals("true", StringComparison.OrdinalIgnoreCase);
        return defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        if (_values.TryGetValue(key, out var val) && int.TryParse(val, out int result))
            return result;
        return defaultValue;
    }

    public void Set(string key, string value)
    {
        _values[key] = value;
        Save();
    }

    /// <summary>
    /// Returns all current settings as key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAll() => _values;

    /// <summary>
    /// Resolve the actual rootfs archive path. If rootfs_path is set,
    /// use it (resolving relative paths against the exe directory).
    /// Otherwise use the default AppData location.
    /// </summary>
    public string ResolveRootfsPath()
    {
        string custom = RootfsPath;
        if (!string.IsNullOrWhiteSpace(custom))
        {
            if (Path.IsPathRooted(custom))
                return custom;
            return Path.Combine(AppContext.BaseDirectory, custom);
        }

        // Default: AppData/Roaming/NetNIX/rootfs.zip
        string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NetNIX");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "rootfs.zip");
    }

    /// <summary>
    /// The full path to the config file on disk.
    /// </summary>
    public string ConfigFilePath => _configPath;

    // --- Internal ---

    private void Load()
    {
        _values.Clear();

        if (!File.Exists(_configPath))
            CreateDefault();

        foreach (var rawLine in File.ReadAllLines(_configPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq].Trim();
            string val = line[(eq + 1)..].Trim();
            _values[key] = val;
        }
    }

    private void Save()
    {
        // Preserve comments from the existing file, update values in-place
        var lines = new List<string>();
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(_configPath))
        {
            foreach (var rawLine in File.ReadAllLines(_configPath))
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
                    string key = trimmed[..eq].Trim();
                    if (_values.TryGetValue(key, out var val))
                    {
                        lines.Add($"{key}={val}");
                        written.Add(key);
                        continue;
                    }
                }
                lines.Add(rawLine);
            }
        }

        // Append any new keys not already in the file
        foreach (var kv in _values)
        {
            if (!written.Contains(kv.Key))
                lines.Add($"{kv.Key}={kv.Value}");
        }

        File.WriteAllLines(_configPath, lines);
    }

    private void CreateDefault()
    {
        var lines = new List<string>
        {
            "# NetNIX Environment Configuration",
            "# This file controls host-side settings for the NetNIX environment.",
            "# Edit this file or use 'nxconfig' inside NetNIX to change settings.",
            "#",
            "# Changes to rootfs_path and allow_reset take effect on next boot.",
            "# Most other settings take effect immediately.",
            "#",
            "# Each setting shows its default value. To restore a setting,",
            "# delete the line or set it back to the documented default.",
            "",
            "# ?? Reset & Recovery ????????????????????????????????????",
            "# Allow Ctrl+R reset at boot and __reset__ login.",
            "# Set to false to disable all environment reset options.",
            $"# Default: {Defaults["allow_reset"]}",
            $"allow_reset={Defaults["allow_reset"]}",
            "",
            "# ?? Filesystem ??????????????????????????????????????????",
            "# Path to the rootfs archive. Leave empty for the default",
            "# location (AppData/Roaming/NetNIX/rootfs.zip).",
            "# Can be an absolute path or relative to the executable.",
            $"# Default: (empty — uses AppData/Roaming/NetNIX/rootfs.zip)",
            $"rootfs_path={Defaults["rootfs_path"]}",
            "",
            "# ?? Instance ????????????????????????????????????????????",
            "# A human-readable name for this NetNIX instance.",
            "# Available to scripts via the nxconfig command.",
            $"# Default: {Defaults["instance_name"]}",
            $"instance_name={Defaults["instance_name"]}",
            "",
            "# ?? Boot ????????????????????????????????????????????????",
            "# Show the boot banner (splash screen) on startup.",
            $"# Default: {Defaults["show_boot_banner"]}",
            $"show_boot_banner={Defaults["show_boot_banner"]}",
            "",
            "# Seconds to wait for Ctrl+R at boot (0 = don't wait).",
            $"# Default: {Defaults["boot_timeout"]}",
            $"boot_timeout={Defaults["boot_timeout"]}",
            "",
            "# ?? Login ???????????????????????????????????????????????",
            "# Text displayed at the login prompt.",
            $"# Default: {Defaults["login_banner"]}",
            $"login_banner={Defaults["login_banner"]}",
            "",
            "# ?? Display ?????????????????????????????????????????????",
            "# Show /etc/motd after boot.",
            $"# Default: {Defaults["motd_enabled"]}",
            $"motd_enabled={Defaults["motd_enabled"]}",
            "",
            "# ?? Persistence ?????????????????????????????????????????",
            "# Auto-save VFS on logout. If false, changes are only",
            "# saved when scripts call api.Save() explicitly.",
            $"# Default: {Defaults["auto_save"]}",
            $"auto_save={Defaults["auto_save"]}",
        };

        File.WriteAllLines(_configPath, lines);
    }
}
