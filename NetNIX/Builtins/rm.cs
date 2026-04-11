using System;
using System.Linq;
using NetNIX.Scripting;

public static class RmCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();
        bool recursive = argList.Remove("-r") | argList.Remove("-rf") | argList.Remove("-fr");
        bool force = argList.Remove("-f") || args.Any(a => a.Contains('f'));

        if (argList.Count == 0)
        {
            Console.WriteLine("rm: missing operand");
            return 1;
        }

        int exitCode = 0;
        foreach (var path in argList)
        {
            if (!api.Exists(path))
            {
                if (!force) Console.WriteLine($"rm: {path}: No such file or directory");
                exitCode = 1;
                continue;
            }
            if (api.IsDirectory(path) && !recursive)
            {
                Console.WriteLine($"rm: cannot remove '{path}': Is a directory");
                exitCode = 1;
                continue;
            }
            api.Delete(path);
        }
        api.Save();
        return exitCode;
    }
}
