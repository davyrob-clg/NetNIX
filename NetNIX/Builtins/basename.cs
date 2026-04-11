using System;
using NetNIX.Scripting;

public static class BasenameCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("basename: missing operand");
            return 1;
        }
        string name = api.GetName(args[0]);
        if (args.Length > 1)
        {
            string suffix = args[1];
            if (name.EndsWith(suffix))
                name = name.Substring(0, name.Length - suffix.Length);
        }
        Console.WriteLine(name);
        return 0;
    }
}
