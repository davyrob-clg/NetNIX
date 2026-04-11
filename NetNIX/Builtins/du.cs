using System;
using System.Linq;
using NetNIX.Scripting;

public static class DuCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();
        bool summary = argList.Remove("-s");
        bool humanReadable = argList.Remove("-h");

        string target = argList.Count > 0 ? argList[0] : ".";
        string resolved = api.ResolvePath(target);

        var allPaths = api.GetAllPaths();
        var dirSizes = new System.Collections.Generic.Dictionary<string, long>();

        foreach (var path in allPaths)
        {
            if (!path.StartsWith(resolved)) continue;
            if (api.IsDirAbsolute(path)) continue;

            int size = api.GetSizeAbsolute(path);
            if (size < 0) size = 0;

            // Attribute to each ancestor directory
            string dir = api.GetParent(path);
            while (dir.Length >= resolved.Length)
            {
                if (!dirSizes.ContainsKey(dir)) dirSizes[dir] = 0;
                dirSizes[dir] += size;
                if (dir == resolved || dir == "/") break;
                dir = api.GetParent(dir);
            }
        }

        if (summary)
        {
            long total = dirSizes.ContainsKey(resolved) ? dirSizes[resolved] : 0;
            Console.WriteLine($"{FormatSize(total, humanReadable)}\t{resolved}");
        }
        else
        {
            foreach (var kv in dirSizes.OrderBy(kv => kv.Key))
                Console.WriteLine($"{FormatSize(kv.Value, humanReadable)}\t{kv.Key}");
        }
        return 0;
    }

    private static string FormatSize(long bytes, bool human)
    {
        if (!human) return bytes.ToString();
        if (bytes < 1024) return bytes + "B";
        if (bytes < 1024 * 1024) return (bytes / 1024) + "K";
        return (bytes / (1024 * 1024)) + "M";
    }
}
