using System;
using NetNIX.Scripting;

public static class DirnameCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("dirname: missing operand");
            return 1;
        }
        Console.WriteLine(api.GetParent(api.ResolvePath(args[0])));
        return 0;
    }
}
