using System;
using System.Linq;
using NetNIX.Scripting;

public static class MkdirCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();
        bool parents = argList.Remove("-p");

        if (argList.Count == 0)
        {
            Console.WriteLine("mkdir: missing operand");
            return 1;
        }

        foreach (var dir in argList)
        {
            if (api.Exists(dir))
            {
                if (!parents)
                    Console.WriteLine($"mkdir: cannot create directory '{dir}': File exists");
                continue;
            }
            if (parents)
                api.CreateDirWithParents(dir);
            else
                api.CreateDir(dir);
        }
        api.Save();
        return 0;
    }
}
