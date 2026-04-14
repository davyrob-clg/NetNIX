namespace NetNIX.Setup;

/// <summary>
/// Loads factory files from the Factory/ directory that ships alongside the
/// NetNIX binary. These files are plain content files copied to the output
/// directory preserving their subdirectory structure. During setup they are
/// installed into the VFS at matching paths.
///
/// For example, Factory/etc/opt/koder/commandprompt.txt is installed to
/// /etc/opt/koder/commandprompt.txt in the virtual filesystem.
/// </summary>
public static class FactoryFiles
{
    private static readonly string FactoryDir =
        Path.Combine(AppContext.BaseDirectory, "Factory");

    /// <summary>
    /// Returns a dictionary mapping VFS install paths to the file content
    /// (as byte arrays) read from the on-disk Factory/ directory.
    /// The Factory/ prefix is stripped so Factory/etc/opt/foo.txt becomes
    /// /etc/opt/foo.txt.
    /// </summary>
    public static Dictionary<string, byte[]> LoadAll()
    {
        var files = new Dictionary<string, byte[]>();

        if (!Directory.Exists(FactoryDir))
            return files;

        foreach (var file in Directory.GetFiles(FactoryDir, "*", SearchOption.AllDirectories))
        {
            // Get the path relative to the Factory directory
            string relative = Path.GetRelativePath(FactoryDir, file);

            // Convert backslashes to forward slashes for VFS path
            string vfsPath = "/" + relative.Replace('\\', '/');

            byte[] data = File.ReadAllBytes(file);
            files[vfsPath] = data;
        }

        return files;
    }

    /// <summary>
    /// Returns all unique directory paths that need to be created in the VFS
    /// to support the factory files.
    /// </summary>
    public static HashSet<string> GetDirectories()
    {
        var dirs = new HashSet<string>(StringComparer.Ordinal);

        if (!Directory.Exists(FactoryDir))
            return dirs;

        foreach (var dir in Directory.GetDirectories(FactoryDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(FactoryDir, dir);
            string vfsPath = "/" + relative.Replace('\\', '/');
            dirs.Add(vfsPath);
        }

        return dirs;
    }
}
