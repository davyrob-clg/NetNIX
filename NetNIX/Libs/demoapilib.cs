using System;
using System;
using NetNIX.Scripting;

/// <summary>
/// demoapilib — A demonstration API library for NetNIX scripts.
///
/// Include in your scripts with:
///     #include &lt;demoapilib&gt;
///
/// Provides helper classes for common tasks like formatted output,
/// file utilities, and system info gathering.
/// </summary>
public static class DemoApiLib
{
    // ?? Formatted Output ???????????????????????????????????????????

    /// <summary>Print a colored header bar with a title.</summary>
    public static void PrintHeader(string title)
    {
        string bar = new string('?', 40);
        Console.WriteLine($"\u001b[1;33m  {bar}\u001b[0m");
        Console.WriteLine($"\u001b[1;33m  {title}\u001b[0m");
        Console.WriteLine($"\u001b[1;33m  {bar}\u001b[0m");
    }

    /// <summary>Print a labeled value with color.</summary>
    public static void PrintValue(string label, object value)
    {
        Console.WriteLine($"  \u001b[36m{label}\u001b[0m = \u001b[1;37m{value}\u001b[0m");
    }

    /// <summary>Print a success message in green.</summary>
    public static void PrintOk(string message)
    {
        Console.WriteLine($"  \u001b[32m? {message}\u001b[0m");
    }

    /// <summary>Print an error message in red.</summary>
    public static void PrintError(string message)
    {
        Console.WriteLine($"  \u001b[31m? {message}\u001b[0m");
    }

    /// <summary>Print an info message in cyan.</summary>
    public static void PrintInfo(string message)
    {
        Console.WriteLine($"  \u001b[36m? {message}\u001b[0m");
    }

    /// <summary>Print a blank separator line.</summary>
    public static void Separator()
    {
        Console.WriteLine();
    }

    // ?? File Utilities ?????????????????????????????????????????????

    /// <summary>Count lines in a VFS text file.</summary>
    public static int CountLines(NixApi api, string path)
    {
        if (!api.IsFile(path)) return -1;
        return api.ReadText(path).Split('\n').Length;
    }

    /// <summary>Get file extension (e.g. ".cs") or empty string.</summary>
    public static string GetExtension(string path)
    {
        string name = path.Contains('/') ? path.Substring(path.LastIndexOf('/') + 1) : path;
        int dot = name.LastIndexOf('.');
        return dot >= 0 ? name.Substring(dot) : "";
    }

    /// <summary>Check if a path is a C# script.</summary>
    public static bool IsCsFile(string path) => GetExtension(path) == ".cs";

    /// <summary>Check if a path is a shell script.</summary>
    public static bool IsShFile(string path) => GetExtension(path) == ".sh";

    /// <summary>
    /// List all files in a directory matching an extension filter.
    /// Pass null for ext to match all files.
    /// </summary>
    public static string[] FindByExtension(NixApi api, string dir, string ext)
    {
        var results = new System.Collections.Generic.List<string>();
        if (!api.IsDirectory(dir)) return results.ToArray();
        foreach (var entry in api.ListDirectory(dir))
        {
            if (api.IsDir(entry)) continue;
            if (ext == null || entry.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                results.Add(entry);
        }
        return results.ToArray();
    }

    // ?? System Info ????????????????????????????????????????????????

    /// <summary>Gather a summary of the current environment.</summary>
    public static string[] GetSystemSummary(NixApi api)
    {
        return new[]
        {
            $"User: {api.Username} (uid={api.Uid}, gid={api.Gid})",
            $"Home: {api.ResolvePath("~")}",
            $"CWD:  {api.Cwd}",
            $"Users: {api.UserCount}, Groups: {api.GroupCount}",
        };
    }

    /// <summary>Calculate total size of all files under a directory.</summary>
    public static long DirectorySize(NixApi api, string dir)
    {
        string resolved = api.ResolvePath(dir);
        long total = 0;
        foreach (var path in api.GetAllPaths())
        {
            if (!path.StartsWith(resolved)) continue;
            if (api.IsDirAbsolute(path)) continue;
            int sz = api.GetSizeAbsolute(path);
            if (sz > 0) total += sz;
        }
        return total;
    }

    // ?? String Utilities ???????????????????????????????????????????

    /// <summary>Pad a string to a fixed width, truncating if needed.</summary>
    public static string Pad(string text, int width)
    {
        if (text.Length >= width) return text.Substring(0, width);
        return text.PadRight(width);
    }

    /// <summary>Create a simple table row from columns.</summary>
    public static string TableRow(params (string text, int width)[] columns)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (text, width) in columns)
        {
            sb.Append(Pad(text, width));
            sb.Append("  ");
        }
        return sb.ToString().TrimEnd();
    }
}
