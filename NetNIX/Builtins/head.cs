using System;
using System.Linq;
using NetNIX.Scripting;

public static class HeadCommand
{
    public static int Run(NixApi api, string[] args)
    {
        int count = 10;
        var files = new System.Collections.Generic.List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-n" && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out count);
            }
            else if (args[i].StartsWith("-") && int.TryParse(args[i].Substring(1), out int n))
            {
                count = n;
            }
            else
            {
                files.Add(args[i]);
            }
        }

        if (files.Count == 0)
        {
            Console.WriteLine("head: missing operand");
            return 1;
        }

        bool multi = files.Count > 1;
        foreach (var file in files)
        {
            if (!api.IsFile(file))
            {
                Console.WriteLine($"head: {file}: No such file");
                continue;
            }
            if (multi) Console.WriteLine($"==> {file} <==");
            var lines = api.ReadText(file).Split('\n');
            foreach (var line in lines.Take(count))
                Console.WriteLine(line);
        }
        return 0;
    }
}
