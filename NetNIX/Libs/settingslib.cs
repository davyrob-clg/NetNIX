using System;
using System.Collections.Generic;
using NetNIX.Scripting;

/// <summary>
/// settingslib — Application settings library for NetNIX scripts.
///
/// Include in your scripts with:
///     #include &lt;settingslib&gt;
///
/// Provides a simple key=value settings store for scripts.
///
/// Per-user settings:    ~/.config/&lt;appname&gt;.conf
/// System-wide settings: /etc/opt/&lt;appname&gt;.conf
///
/// Usage:
///     Settings.Set(api, "myapp", "theme", "dark");
///     string theme = Settings.Get(api, "myapp", "theme", "light");
///     Settings.SetSystem(api, "myapp", "max_users", "100");
///     string max = Settings.GetSystem(api, "myapp", "max_users");
/// </summary>
public static class Settings
{
    // ?? Per-user settings ????????????????????????????????????????????

    /// <summary>
    /// Get a per-user setting value. Returns null if not found.
    /// Settings are stored in ~/.config/&lt;appName&gt;.conf
    /// </summary>
    public static string Get(NixApi api, string appName, string key)
    {
        var dict = LoadUser(api, appName);
        return dict.ContainsKey(key) ? dict[key] : null;
    }

    /// <summary>
    /// Get a per-user setting value with a default fallback.
    /// </summary>
    public static string Get(NixApi api, string appName, string key, string defaultValue)
    {
        return Get(api, appName, key) ?? defaultValue;
    }

    /// <summary>
    /// Set a per-user setting value.
    /// </summary>
    public static void Set(NixApi api, string appName, string key, string value)
    {
        var dict = LoadUser(api, appName);
        dict[key] = value;
        SaveUser(api, appName, dict);
    }

    /// <summary>
    /// Remove a per-user setting key.
    /// Returns true if the key existed.
    /// </summary>
    public static bool Remove(NixApi api, string appName, string key)
    {
        var dict = LoadUser(api, appName);
        if (!dict.ContainsKey(key)) return false;
        dict.Remove(key);
        SaveUser(api, appName, dict);
        return true;
    }

    /// <summary>
    /// Get all per-user settings for an app as key-value pairs.
    /// </summary>
    public static Dictionary<string, string> GetAll(NixApi api, string appName)
    {
        return LoadUser(api, appName);
    }

    /// <summary>
    /// Delete all per-user settings for an app.
    /// </summary>
    public static void Clear(NixApi api, string appName)
    {
        string path = UserPath(api, appName);
        if (api.IsFile(path))
            api.Delete(path);
    }

    /// <summary>
    /// List app names that have per-user settings for the current user.
    /// </summary>
    public static string[] ListApps(NixApi api)
    {
        string configDir = UserConfigDir(api);
        if (!api.IsDirectory(configDir))
            return new string[0];

        var apps = new List<string>();
        foreach (var entry in api.ListDirectory(configDir))
        {
            string name = api.NodeName(entry);
            if (name.EndsWith(".conf"))
                apps.Add(name.Substring(0, name.Length - 5));
        }
        return apps.ToArray();
    }

    // ?? System-wide settings ?????????????????????????????????????????

    /// <summary>
    /// Get a system-wide setting value. Returns null if not found.
    /// Settings are stored in /etc/opt/&lt;appName&gt;.conf
    /// </summary>
    public static string GetSystem(NixApi api, string appName, string key)
    {
        var dict = LoadSystem(api, appName);
        return dict.ContainsKey(key) ? dict[key] : null;
    }

    /// <summary>
    /// Get a system-wide setting with a default fallback.
    /// </summary>
    public static string GetSystem(NixApi api, string appName, string key, string defaultValue)
    {
        return GetSystem(api, appName, key) ?? defaultValue;
    }

    /// <summary>
    /// Set a system-wide setting value.
    /// Requires root (uid 0). Non-root callers see an error message.
    /// </summary>
    public static bool SetSystem(NixApi api, string appName, string key, string value)
    {
        if (api.Uid != 0)
        {
            Console.Error.WriteLine("settings: system settings require root");
            return false;
        }
        var dict = LoadSystem(api, appName);
        dict[key] = value;
        SaveSystem(api, appName, dict);
        return true;
    }

    /// <summary>
    /// Remove a system-wide setting key. Requires root.
    /// </summary>
    public static bool RemoveSystem(NixApi api, string appName, string key)
    {
        if (api.Uid != 0)
        {
            Console.Error.WriteLine("settings: system settings require root");
            return false;
        }
        var dict = LoadSystem(api, appName);
        if (!dict.ContainsKey(key)) return false;
        dict.Remove(key);
        SaveSystem(api, appName, dict);
        return true;
    }

    /// <summary>
    /// Get all system-wide settings for an app.
    /// </summary>
    public static Dictionary<string, string> GetAllSystem(NixApi api, string appName)
    {
        return LoadSystem(api, appName);
    }

    /// <summary>
    /// Delete all system-wide settings for an app. Requires root.
    /// </summary>
    public static bool ClearSystem(NixApi api, string appName)
    {
        if (api.Uid != 0)
        {
            Console.Error.WriteLine("settings: system settings require root");
            return false;
        }
        string path = SystemPath(appName);
        if (api.IsFile(path))
            api.Delete(path);
        return true;
    }

    /// <summary>
    /// List app names that have system-wide settings.
    /// </summary>
    public static string[] ListSystemApps(NixApi api)
    {
        string dir = "/etc/opt";
        if (!api.IsDirectory(dir))
            return new string[0];

        var apps = new List<string>();
        foreach (var entry in api.ListDirectory(dir))
        {
            string name = api.NodeName(entry);
            if (name.EndsWith(".conf"))
                apps.Add(name.Substring(0, name.Length - 5));
        }
        return apps.ToArray();
    }

    // ?? Helpers ??????????????????????????????????????????????????????

    /// <summary>
    /// Get the effective value for a key: per-user overrides system-wide.
    /// Falls back to system setting if no per-user setting exists.
    /// </summary>
    public static string GetEffective(NixApi api, string appName, string key, string defaultValue = null)
    {
        string val = Get(api, appName, key);
        if (val != null) return val;
        val = GetSystem(api, appName, key);
        if (val != null) return val;
        return defaultValue;
    }

    // ?? Internal ????????????????????????????????????????????????????

    private static string UserConfigDir(NixApi api)
    {
        // Resolve ~ to the user's home directory
        string home = api.Uid == 0 ? "/root" : "/home/" + api.Username;
        return home + "/.config";
    }

    private static string UserPath(NixApi api, string appName)
    {
        return UserConfigDir(api) + "/" + appName + ".conf";
    }

    private static string SystemPath(string appName)
    {
        return "/etc/opt/" + appName + ".conf";
    }

    private static Dictionary<string, string> LoadUser(NixApi api, string appName)
    {
        return LoadConf(api, UserPath(api, appName));
    }

    private static Dictionary<string, string> LoadSystem(NixApi api, string appName)
    {
        return LoadConf(api, SystemPath(appName));
    }

    private static void SaveUser(NixApi api, string appName, Dictionary<string, string> dict)
    {
        string path = UserPath(api, appName);
        string dir = UserConfigDir(api);
        if (!api.IsDirectory(dir))
            api.CreateDirWithParents(dir);
        SaveConf(api, path, dict);
    }

    private static void SaveSystem(NixApi api, string appName, Dictionary<string, string> dict)
    {
        SaveConf(api, SystemPath(appName), dict);
    }

    private static Dictionary<string, string> LoadConf(NixApi api, string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!api.IsFile(path))
            return dict;

        string content = api.ReadText(path);
        foreach (var rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim();
            string val = line.Substring(eq + 1).Trim();
            dict[key] = val;
        }
        return dict;
    }

    private static void SaveConf(NixApi api, string path, Dictionary<string, string> dict)
    {
        string content = "# Settings file — managed by settingslib\n";
        foreach (var kv in dict)
            content += kv.Key + "=" + kv.Value + "\n";
        api.WriteText(path, content);
        api.Save();
    }
}
