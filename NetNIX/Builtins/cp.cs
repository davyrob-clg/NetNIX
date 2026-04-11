using System;
using NetNIX.Scripting;

public static class CpCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("cp: usage: cp [-r] <source> <dest>");
            return 1;
        }

        var argList = new System.Collections.Generic.List<string>(args);
        bool recursive = argList.Remove("-r") | argList.Remove("-R");

        if (argList.Count < 2)
        {
            Console.WriteLine("cp: missing destination");
            return 1;
        }

        string src = argList[0];
        string dest = argList[1];

        if (!api.Exists(src))
        {
            Console.WriteLine($"cp: {src}: No such file or directory");
            return 1;
        }

        if (api.IsDirectory(src) && !recursive)
        {
            Console.WriteLine($"cp: -r not specified; omitting directory '{src}'");
            return 1;
        }

        api.Copy(src, dest);
        api.Save();
        return 0;
    }
}
