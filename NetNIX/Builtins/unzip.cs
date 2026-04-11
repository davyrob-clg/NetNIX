using System;
using System.Linq;
using NetNIX.Scripting;

public static class UnzipCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        if (argList.Count == 0 || argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return argList.Count == 0 ? 1 : 0;
        }

        bool listMode = argList.Remove("-l") || argList.Remove("--list");

        if (argList.Count == 0)
        {
            Console.WriteLine("unzip: no archive specified");
            return 1;
        }

        string zipPath = argList[0];

        if (!api.IsFile(zipPath))
        {
            Console.WriteLine($"unzip: {zipPath}: No such file");
            return 1;
        }

        if (listMode)
        {
            var entries = api.ZipList(zipPath);
            if (entries == null) return 1;

            Console.WriteLine($"Archive: {zipPath}");
            Console.WriteLine($"{"Length",10}  Name");
            Console.WriteLine($"{"??????????",10}  ????????????????????");

            long total = 0;
            foreach (var (name, _, uncompressed) in entries)
            {
                Console.WriteLine($"{uncompressed,10}  {name}");
                total += uncompressed;
            }

            Console.WriteLine($"{"??????????",10}  ????????????????????");
            Console.WriteLine($"{total,10}  {entries.Length} entries");
            return 0;
        }

        // Determine output directory
        string destDir;
        if (argList.Count > 1)
        {
            destDir = argList[1];
        }
        else
        {
            // Default: current directory
            destDir = ".";
        }

        Console.WriteLine($"unzip: extracting {zipPath} to {destDir}");

        int count = api.ZipExtract(zipPath, destDir);
        if (count < 0) return 1;

        Console.WriteLine($"unzip: extracted {count} files");
        api.Save();
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("unzip - extract files from a zip archive");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  unzip <archive.zip> [dest-dir]   Extract to directory");
        Console.WriteLine("  unzip -l <archive.zip>           List contents");
        Console.WriteLine();
        Console.WriteLine("  If no destination is given, files are extracted to");
        Console.WriteLine("  the current directory.");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  unzip backup.zip");
        Console.WriteLine("  unzip backup.zip /tmp/extracted");
        Console.WriteLine("  unzip -l backup.zip");
    }
}
