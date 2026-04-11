using System;
using NetNIX.Scripting;

public static class RmdirCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("rmdir: missing operand");
            return 1;
        }

        foreach (var dir in args)
        {
            if (!api.IsDirectory(dir))
            {
                Console.WriteLine($"rmdir: failed to remove '{dir}': Not a directory");
                continue;
            }
            var children = api.ListDirectory(dir);
            if (children.Length > 0)
            {
                Console.WriteLine($"rmdir: failed to remove '{dir}': Directory not empty");
                continue;
            }
            api.Delete(dir);
        }
        api.Save();
        return 0;
    }
}
