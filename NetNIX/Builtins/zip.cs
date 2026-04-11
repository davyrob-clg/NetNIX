using System;
using System.Linq;
using NetNIX.Scripting;

public static class ZipCommand
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
        bool addMode = argList.Remove("-a") || argList.Remove("--add");

        if (listMode)
        {
            if (argList.Count == 0)
            {
                Console.WriteLine("zip: -l requires a zip file path");
                return 1;
            }
            return ListZip(api, argList[0]);
        }

        if (argList.Count < 2)
        {
            Console.WriteLine("zip: need at least a zip file and one source");
            Console.WriteLine("Usage: zip <archive.zip> <file|dir> [file|dir ...]");
            return 1;
        }

        string zipPath = argList[0];
        var sources = argList.Skip(1).ToArray();

        if (addMode)
        {
            // Add files to existing archive
            foreach (var src in sources)
            {
                if (!api.Exists(src))
                {
                    Console.WriteLine($"zip: {src}: No such file or directory");
                    return 1;
                }
                bool ok = api.ZipAddFile(zipPath, src);
                if (!ok) return 1;
                Console.WriteLine($"  adding: {src}");
            }
        }
        else
        {
            // Create new archive
            foreach (var src in sources)
            {
                if (!api.Exists(src))
                {
                    Console.WriteLine($"zip: {src}: No such file or directory");
                    return 1;
                }
            }

            bool ok = api.ZipCreate(zipPath, sources);
            if (!ok) return 1;

            foreach (var src in sources)
            {
                string type = api.IsDirectory(src) ? "dir" : "file";
                Console.WriteLine($"  adding: {src} ({type})");
            }
        }

        int size = api.GetSize(zipPath);
        Console.WriteLine($"zip: created {zipPath} ({size} bytes)");
        api.Save();
        return 0;
    }

    private static int ListZip(NixApi api, string zipPath)
    {
        if (!api.IsFile(zipPath))
        {
            Console.WriteLine($"zip: {zipPath}: No such file");
            return 1;
        }

        var entries = api.ZipList(zipPath);
        if (entries == null) return 1;

        Console.WriteLine($"Archive: {zipPath}");
        Console.WriteLine($"{"Length",10}  {"Compressed",10}  Name");
        Console.WriteLine($"{"??????????",10}  {"??????????",10}  ????????????????????");

        long totalSize = 0;
        long totalCompressed = 0;
        foreach (var (name, compressed, uncompressed) in entries)
        {
            Console.WriteLine($"{uncompressed,10}  {compressed,10}  {name}");
            totalSize += uncompressed;
            totalCompressed += compressed;
        }

        Console.WriteLine($"{"??????????",10}  {"??????????",10}  ????????????????????");
        Console.WriteLine($"{totalSize,10}  {totalCompressed,10}  {entries.Length} entries");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("zip - create and manage zip archives");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  zip <archive.zip> <file|dir> [...]   Create archive");
        Console.WriteLine("  zip -a <archive.zip> <file> [...]    Add files to archive");
        Console.WriteLine("  zip -l <archive.zip>                 List contents");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  zip backup.zip file1.txt file2.txt");
        Console.WriteLine("  zip project.zip /home/user/src");
        Console.WriteLine("  zip -a backup.zip newfile.txt");
        Console.WriteLine("  zip -l backup.zip");
    }
}
