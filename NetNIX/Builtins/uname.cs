using System;
using NetNIX.Scripting;

public static class UnameCommand
{
    public static int Run(NixApi api, string[] args)
    {
        bool all = args.Length > 0 && (args[0] == "-a" || args[0] == "--all");

        if (all)
            Console.WriteLine("NetNIX 1.0.0 netnix NetNIX-VFS .NET8 nsh");
        else if (args.Length > 0 && args[0] == "-r")
            Console.WriteLine("1.0.0");
        else if (args.Length > 0 && args[0] == "-s")
            Console.WriteLine("NetNIX");
        else if (args.Length > 0 && args[0] == "-n")
            Console.WriteLine("netnix");
        else
            Console.WriteLine("NetNIX");

        return 0;
    }
}
