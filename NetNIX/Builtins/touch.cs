using System;
using NetNIX.Scripting;

public static class TouchCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("touch: missing operand");
            return 1;
        }

        foreach (var file in args)
        {
            if (!api.Exists(file))
                api.CreateEmptyFile(file);
        }
        api.Save();
        return 0;
    }
}
