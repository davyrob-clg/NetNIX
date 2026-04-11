using System;
using NetNIX.Scripting;

public static class DfCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var allPaths = api.GetAllPaths();
        long totalSize = 0;
        int fileCount = 0;
        int dirCount = 0;

        foreach (var path in allPaths)
        {
            if (api.IsDirAbsolute(path))
            {
                dirCount++;
            }
            else
            {
                fileCount++;
                int sz = api.GetSizeAbsolute(path);
                if (sz > 0) totalSize += sz;
            }
        }

        Console.WriteLine("Filesystem      Size    Files   Dirs");
        Console.WriteLine($"{"netnix-vfs",-16}{FormatSize(totalSize),7} {fileCount,7} {dirCount,6}");
        return 0;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return bytes + "B";
        if (bytes < 1024 * 1024) return (bytes / 1024) + "K";
        return (bytes / (1024 * 1024)) + "M";
    }
}
