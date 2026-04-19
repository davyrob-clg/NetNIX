using System;
using NetNIX.Scripting;

public static class ReinstallCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            PrintUsage();
            return 0;
        }

        if (api.Uid != 0)
        {
            Console.WriteLine("reinstall: permission denied (must be root)");
            return 1;
        }

        Console.WriteLine("Reinstalling factory binaries, libraries, and man pages...");

        if (api.ReinstallFactory())
        {
            Console.WriteLine("reinstall: factory files restored successfully");
            return 0;
        }

        Console.WriteLine("reinstall: failed");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("reinstall - restore factory binaries, libraries, and man pages");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  reinstall");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help    Show this help");
        Console.WriteLine();
        Console.WriteLine("Description:");
        Console.WriteLine("  Overwrites /bin, /sbin, /lib, and /usr/share/man with the");
        Console.WriteLine("  factory versions shipped with the NetNIX executable.");
        Console.WriteLine();
        Console.WriteLine("  This is useful after an update to restore new or fixed commands,");
        Console.WriteLine("  or if built-in files have been accidentally modified or deleted.");
        Console.WriteLine();
        Console.WriteLine("  User files, home directories, and settings are NOT affected.");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Only root can run this command.");
    }
}
