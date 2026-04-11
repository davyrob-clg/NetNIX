using System;
using NetNIX.Scripting;

public static class EchoCommand
{
    public static int Run(NixApi api, string[] args)
    {
        bool noNewline = false;
        int start = 0;
        if (args.Length > 0 && args[0] == "-n")
        {
            noNewline = true;
            start = 1;
        }
        string text = string.Join(' ', args, start, args.Length - start);
        if (noNewline)
            Console.Write(text);
        else
            Console.WriteLine(text);
        return 0;
    }
}
