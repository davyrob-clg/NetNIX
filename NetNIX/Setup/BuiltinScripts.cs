namespace NetNIX.Setup;

/// <summary>
/// Loads built-in command scripts from the Builtins/ directory that ships
/// alongside the NetNIX binary. These .cs files are not compiled into the
/// assembly — they are plain-text content files copied to the output directory.
/// </summary>
public static class BuiltinScripts
{
    private static readonly string BuiltinsDir =
        Path.Combine(AppContext.BaseDirectory, "Builtins");

    /// <summary>
    /// Returns a dictionary mapping VFS install paths (/bin/name.cs) to
    /// the source code read from the on-disk Builtins/ directory.
    /// </summary>
    public static Dictionary<string, string> LoadAll()
    {
        var scripts = new Dictionary<string, string>();

        if (!Directory.Exists(BuiltinsDir))
        {
            Console.WriteLine($"Warning: Builtins directory not found at {BuiltinsDir}");
            return scripts;
        }

        foreach (var file in Directory.GetFiles(BuiltinsDir, "*.cs"))
        {
            string filename = Path.GetFileName(file);
            string vfsPath = "/bin/" + filename;
            string source = File.ReadAllText(file);
            scripts[vfsPath] = source;
        }

        return scripts;
    }

    /// <summary>
    /// Reads a single builtin script by name (e.g. "ls" reads Builtins/ls.cs).
    /// Returns null if the file is not found.
    /// </summary>
    public static string? Load(string name)
    {
        string path = Path.Combine(BuiltinsDir, name + ".cs");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
