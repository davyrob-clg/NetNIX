using System;
using System.Linq;
using NetNIX.Scripting;

public static class ExportCommand
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
            Console.WriteLine("export: permission denied (must be root)");
            return 1;
        }

        bool includeMounts = argList.Remove("--mounts") | argList.Remove("-m");

        if (argList.Count == 0)
        {
            Console.WriteLine("export: no host path specified");
            return 1;
        }

        string hostPath = argList[0];
        string vfsRoot = argList.Count > 1 ? argList[1] : "/";

        if (!api.IsDirectory(vfsRoot))
        {
            Console.WriteLine($"export: {vfsRoot}: not a directory");
            return 1;
        }

        try
        {
            int count = api.ExportFs(hostPath, vfsRoot, includeMounts);
            string suffix = includeMounts ? " (including mounts)" : "";
            Console.WriteLine($"export: exported {count} files to {hostPath}{suffix}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"export: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("export - export the virtual filesystem to a host zip archive");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  export [options] <host-path> [vfs-root]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <host-path>   Path on the host OS for the output zip file");
        Console.WriteLine("  [vfs-root]    VFS directory to export (default: / for entire filesystem)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -m, --mounts  Include mounted filesystems in the export");
        Console.WriteLine("  -h, --help    Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  export C:\\Backups\\netnix-full.zip");
        Console.WriteLine("  export C:\\Backups\\netnix-full.zip /");
        Console.WriteLine("  export --mounts C:\\Backups\\everything.zip");
        Console.WriteLine("  export C:\\Backups\\home-alice.zip /home/alice");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Only root can export the filesystem.");
        Console.WriteLine("  Mounted filesystems (/mnt) are excluded by default.");
        Console.WriteLine("  Use --mounts to include them.");
    }
}
