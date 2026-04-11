using System;
using NetNIX.Scripting;

public static class MvCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("mv: usage: mv <source> <dest>");
            return 1;
        }

        string src = args[0];
        string dest = args[1];

        if (!api.Exists(src))
        {
            Console.WriteLine($"mv: {src}: No such file or directory");
            return 1;
        }

        api.Move(src, dest);
        api.Save();
        return 0;
    }
}
