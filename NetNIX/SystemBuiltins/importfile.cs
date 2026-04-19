using System;
using System.Linq;
using NetNIX.Scripting;

public static class ImportFileCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        if (argList.Count == 0 || argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return argList.Count == 0 ? 1 : 0;
        }

        if (api.Uid != 0)
        {
            Console.WriteLine("importfile: permission denied (must be root)");
            return 1;
        }

        bool dirMode = argList.Remove("-d") || argList.Remove("--directory");

        if (argList.Count == 0)
        {
            Console.WriteLine("importfile: missing host path");
            return 1;
        }

        string hostPath = argList[0];

        if (dirMode)
            return ImportDir(api, hostPath, argList.Count > 1 ? argList[1] : null);
        else
            return ImportSingleFile(api, hostPath, argList.Count > 1 ? argList[1] : null);
    }

    private static int ImportSingleFile(NixApi api, string hostPath, string dest)
    {
        string vfsPath;
        if (dest != null)
        {
            vfsPath = dest;
            if (api.IsDirectory(vfsPath))
            {
                string fileName = HostName(hostPath);
                vfsPath = vfsPath.TrimEnd('/') + "/" + fileName;
            }
        }
        else
        {
            vfsPath = HostName(hostPath);
        }

        if (api.ImportFile(hostPath, vfsPath))
        {
            int size = api.GetSize(vfsPath);
            Console.WriteLine($"importfile: imported {hostPath} -> {api.ResolvePath(vfsPath)} ({size} bytes)");
            api.Save();
            return 0;
        }

        return 1;
    }

    private static int ImportDir(NixApi api, string hostDir, string dest)
    {
        string vfsDir;
        if (dest != null)
        {
            vfsDir = dest;
        }
        else
        {
            string dirName = HostName(hostDir.TrimEnd('/', '\\'));
            vfsDir = dirName;
        }

        int count = api.ImportDirectory(hostDir, vfsDir);
        if (count < 0)
            return 1;

        string resolved = api.ResolvePath(vfsDir);
        Console.WriteLine($"importfile: imported directory {hostDir} -> {resolved} ({count} file(s))");
        api.Save();
        return 0;
    }

    /// <summary>
    /// Extract the trailing name from a host path (handles both / and \ separators).
    /// </summary>
    private static string HostName(string hostPath)
    {
        int lastSep = hostPath.LastIndexOfAny(['/', '\\']);
        return lastSep >= 0 ? hostPath[(lastSep + 1)..] : hostPath;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("importfile - import files or directories from the host into the VFS");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  importfile <host-path> [vfs-path]");
        Console.WriteLine("  importfile -d <host-dir> [vfs-dir]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <host-path>   Full path to a file or directory on the host OS");
        Console.WriteLine("  [vfs-path]    Destination in the VFS (default: current directory)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -d, --directory  Import a directory and all its contents recursively");
        Console.WriteLine("  -h, --help       Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  importfile C:\\Users\\me\\data.zip");
        Console.WriteLine("  importfile C:\\Users\\me\\data.zip /tmp/data.zip");
        Console.WriteLine("  importfile C:\\Users\\me\\notes.txt /home/alice/");
        Console.WriteLine("  importfile -d C:\\Users\\me\\project /home/alice/project");
        Console.WriteLine("  importfile -d C:\\Users\\me\\scripts");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Only root can import from the host.");
        Console.WriteLine("  The host files are not modified (they are copied).");
        Console.WriteLine("  If a VFS destination already exists it is overwritten.");
        Console.WriteLine("  With -d, the directory structure is preserved recursively.");
    }
}
