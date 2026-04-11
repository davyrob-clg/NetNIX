using System;
using System.Collections.Generic;
using NetNIX.Scripting;

/// <summary>
/// ziplib — Zip archive utility library for NetNIX scripts.
///
/// Include in your scripts with:
///     #include &lt;ziplib&gt;
///
/// Provides helper methods for working with zip archives in the VFS.
/// </summary>
public static class ZipLib
{
    /// <summary>
    /// Create a zip from a single directory, returning success status.
    /// </summary>
    public static bool ZipDirectory(NixApi api, string dir, string zipPath)
    {
        if (!api.IsDirectory(dir)) return false;
        return api.ZipCreate(zipPath, dir);
    }

    /// <summary>
    /// Create a zip from all files matching an extension in a directory.
    /// Does not recurse into subdirectories.
    /// </summary>
    public static bool ZipByExtension(NixApi api, string dir, string ext, string zipPath)
    {
        if (!api.IsDirectory(dir)) return false;

        var files = new List<string>();
        foreach (var entry in api.ListDirectory(dir))
        {
            if (api.IsDir(entry)) continue;
            if (entry.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                files.Add(entry);
        }

        if (files.Count == 0) return false;

        // Use absolute VFS paths
        return api.ZipCreate(zipPath, files.ToArray());
    }

    /// <summary>
    /// Extract and return the number of files, or -1 on error.
    /// </summary>
    public static int ExtractTo(NixApi api, string zipPath, string destDir)
    {
        return api.ZipExtract(zipPath, destDir);
    }

    /// <summary>
    /// Get a list of filenames inside a zip archive.
    /// Returns empty array on error.
    /// </summary>
    public static string[] ListEntries(NixApi api, string zipPath)
    {
        var entries = api.ZipList(zipPath);
        if (entries == null) return new string[0];

        var names = new List<string>();
        foreach (var (name, _, _) in entries)
            names.Add(name);
        return names.ToArray();
    }

    /// <summary>
    /// Get the total uncompressed size of all entries in a zip.
    /// Returns -1 on error.
    /// </summary>
    public static long TotalSize(NixApi api, string zipPath)
    {
        var entries = api.ZipList(zipPath);
        if (entries == null) return -1;

        long total = 0;
        foreach (var (_, _, uncompressed) in entries)
            total += uncompressed;
        return total;
    }

    /// <summary>
    /// Get the compression ratio of a zip archive (0.0 to 1.0).
    /// Returns -1 on error.
    /// </summary>
    public static double CompressionRatio(NixApi api, string zipPath)
    {
        var entries = api.ZipList(zipPath);
        if (entries == null) return -1;

        long totalOriginal = 0;
        long totalCompressed = 0;
        foreach (var (_, compressed, uncompressed) in entries)
        {
            totalOriginal += uncompressed;
            totalCompressed += compressed;
        }

        if (totalOriginal == 0) return 0;
        return 1.0 - ((double)totalCompressed / totalOriginal);
    }

    /// <summary>
    /// Check if a zip archive contains a specific entry by name.
    /// </summary>
    public static bool Contains(NixApi api, string zipPath, string entryName)
    {
        var entries = api.ZipList(zipPath);
        if (entries == null) return false;

        foreach (var (name, _, _) in entries)
        {
            if (name.Equals(entryName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Create a backup zip of a directory with a timestamped name.
    /// Returns the zip path, or null on failure.
    /// </summary>
    public static string BackupDirectory(NixApi api, string dir, string backupDir)
    {
        if (!api.IsDirectory(dir)) return null;
        if (!api.IsDirectory(backupDir))
            api.CreateDirWithParents(backupDir);

        string dirName = api.NodeName(api.ResolvePath(dir));
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string zipPath = backupDir.TrimEnd('/') + $"/{dirName}_{timestamp}.zip";

        bool ok = api.ZipCreate(zipPath, dir);
        return ok ? zipPath : null;
    }
}
