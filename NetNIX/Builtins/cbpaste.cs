using System;
using System.Linq;
using NetNIX.Scripting;

public static class CbpasteCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        // Handle help
        if (argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return 0;
        }

        // Handle append mode
        bool append = argList.Remove("-a");

        // Handle explicit print mode
        bool printOnly = argList.Remove("-p");

        // Read clipboard
        string text = api.GetClipboard();
        if (text == null)
        {
            Console.WriteLine("cbpaste: clipboard is empty or unavailable");
            return 1;
        }

        // If no file argument or -p flag, print to stdout
        if (printOnly || argList.Count == 0)
        {
            Console.Write(text);
            if (!text.EndsWith('\n'))
                Console.WriteLine();
            return 0;
        }

        // Write/append to file
        string file = argList[0];

        if (append)
        {
            api.AppendText(file, text);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(text);
            Console.WriteLine($"cbpaste: appended {bytes} bytes to {file}");
        }
        else
        {
            api.WriteText(file, text);
            int bytes = System.Text.Encoding.UTF8.GetByteCount(text);
            Console.WriteLine($"cbpaste: wrote {bytes} bytes to {file}");
        }

        api.Save();
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("cbpaste - paste host clipboard into NetNIX");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  cbpaste             Print clipboard to stdout");
        Console.WriteLine("  cbpaste <file>      Write clipboard to file");
        Console.WriteLine("  cbpaste -a <file>   Append clipboard to file");
        Console.WriteLine("  cbpaste -p          Print clipboard to stdout");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  cbpaste                  View clipboard contents");
        Console.WriteLine("  cbpaste notes.txt        Save clipboard to file");
        Console.WriteLine("  cbpaste -a log.txt       Append clipboard to file");
        Console.WriteLine("  cbpaste mycode.cs        Paste code from host");
    }
}
