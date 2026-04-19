using System;
using System.Linq;
using NetNIX.Scripting;

public static class UmountCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0 || args.Any(a => a == "-h" || a == "--help"))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var argList = args.ToList();
        bool saveChanges = argList.Remove("--save") | argList.Remove("-s");

        if (argList.Count == 0)
        {
            Console.WriteLine("umount: no mount point specified");
            return 1;
        }

        string mountPoint = argList[0];

        if (api.Uid != 0)
        {
            Console.WriteLine("umount: permission denied (must be root)");
            return 1;
        }

        try
        {
            api.Unmount(mountPoint, saveChanges);
            if (saveChanges)
                Console.WriteLine($"umount: saved and unmounted {api.ResolvePath(mountPoint)}");
            else
                Console.WriteLine($"umount: {api.ResolvePath(mountPoint)} unmounted");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"umount: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("umount - unmount a previously mounted archive");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  umount [options] <mount-point>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -s, --save    Save changes back to the host zip before unmounting");
        Console.WriteLine("  -h, --help    Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  umount /mnt/data             Discard changes and unmount");
        Console.WriteLine("  umount --save /mnt/data      Save changes to host zip, then unmount");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Only root can unmount archives.");
        Console.WriteLine("  Without --save, all in-memory changes are discarded.");
        Console.WriteLine("  Use 'mount --sync <point>' to save without unmounting.");
    }
}
