using System;
using NetNIX.Scripting;

public static class GrepCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = new System.Collections.Generic.List<string>(args);
        bool ignoreCase = argList.Remove("-i");
        bool showLineNum = argList.Remove("-n");
        bool invertMatch = argList.Remove("-v");
        bool countOnly = argList.Remove("-c");

        if (argList.Count < 1)
        {
            Console.WriteLine("grep: usage: grep [-inv] [-c] <pattern> [file...]");
            return 1;
        }

        string pattern = argList[0];
        var files = argList.Count > 1
            ? argList.GetRange(1, argList.Count - 1)
            : new System.Collections.Generic.List<string>();

        if (files.Count == 0)
        {
            Console.WriteLine("grep: (reading from stdin not supported, specify file)");
            return 1;
        }

        bool multi = files.Count > 1;
        int found = 0;

        foreach (var file in files)
        {
            if (!api.IsFile(file))
            {
                Console.WriteLine($"grep: {file}: No such file");
                continue;
            }
            var lines = api.ReadText(file).Split('\n');
            int fileCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                bool match = ignoreCase
                    ? lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase)
                    : lines[i].Contains(pattern);

                if (invertMatch) match = !match;

                if (match)
                {
                    found++;
                    fileCount++;
                    if (!countOnly)
                    {
                        string prefix = "";
                        if (multi) prefix += file + ":";
                        if (showLineNum) prefix += (i + 1) + ":";
                        Console.WriteLine(prefix + lines[i]);
                    }
                }
            }
            if (countOnly)
            {
                string prefix = multi ? file + ":" : "";
                Console.WriteLine($"{prefix}{fileCount}");
            }
        }
        return found > 0 ? 0 : 1;
    }
}
