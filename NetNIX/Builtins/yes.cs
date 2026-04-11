using System;
using NetNIX.Scripting;

public static class YesCommand
{
    public static int Run(NixApi api, string[] args)
    {
        string text = args.Length > 0 ? string.Join(' ', args) : "y";
        for (int i = 0; i < 100; i++)
            Console.WriteLine(text);
        return 0;
    }
}
