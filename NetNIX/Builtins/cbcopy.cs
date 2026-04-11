using System;
using System.Linq;
using NetNIX.Scripting;

public static class CbcopyCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        if (argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return 0;
        }

        if (argList.Count == 0)
        {
            Console.WriteLine("cbcopy: usage: cbcopy <file>");
            return 1;
        }

        string file = argList[0];

        if (!api.IsFile(file))
        {
            Console.WriteLine($"cbcopy: {file}: No such file");
            return 1;
        }

        string text = api.ReadText(file);
        bool ok = api.SetClipboard(text);

        if (!ok)
        {
            Console.WriteLine("cbcopy: failed to copy to host clipboard");
            return 1;
        }

        int bytes = System.Text.Encoding.UTF8.GetByteCount(text);
        Console.WriteLine($"cbcopy: copied {bytes} bytes from {file} to host clipboard");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("cbcopy - copy a VFS file to the host clipboard");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  cbcopy <file>       Copy file contents to host clipboard");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  cbcopy notes.txt        Copy notes to clipboard");
        Console.WriteLine("  cbcopy /bin/ls.cs        Copy a script to clipboard");
        Console.WriteLine("  cbcopy ~/.nshrc          Copy startup script to clipboard");
    }
}
