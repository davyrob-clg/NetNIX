/*
test
*/
using System;
using System;
using System.Collections.Generic;
using NetNIX.Scripting;

public static class GrepCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = new List<string>(args);
        bool ignoreCase = argList.Remove("-i");
        bool showLineNum = argList.Remove("-n");
        bool invertMatch = argList.Remove("-v");
        bool countOnly = argList.Remove("-c");

        if (argList.Count < 1)
        {
            Console.WriteLine("grep: usage: grep [-invc] <pattern> [file...]");
            return 1;
        }

        string pattern = argList[0];
        var files = argList.Count > 1
            ? argList.GetRange(1, argList.Count - 1)
            : new List<string>();

        int found = 0;

        if (files.Count == 0)
        {
            // Read from stdin if piped, otherwise error
            if (!Console.IsInputRedirected && !NetNIX.Shell.NixShell.IsPiped)
            {
                Console.WriteLine("grep: usage: grep [-invc] <pattern> [file...]");
                return 1;
            }

            var stdinLines = new List<string>();
            string line;
            while ((line = Console.ReadLine()) != null)
                stdinLines.Add(line);

            int fileCount = 0;
            for (int i = 0; i < stdinLines.Count; i++)
            {
                bool match = ignoreCase
                    ? stdinLines[i].IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0
                    : stdinLines[i].Contains(pattern);

                if (invertMatch) match = !match;

                if (match)
                {
                    found++;
                    fileCount++;
                    if (!countOnly)
                    {
                        string prefix = showLineNum ? (i + 1) + ":" : "";
                        Console.WriteLine(prefix + stdinLines[i]);
                    }
                }
            }
            if (countOnly)
                Console.WriteLine(fileCount);

            return found > 0 ? 0 : 1;
        }

        bool multi = files.Count > 1;

        foreach (var file in files)
        {
            if (!api.IsFile(file))
            {
                Console.WriteLine("grep: " + file + ": No such file");
                continue;
            }
            var lines = api.ReadText(file).Split('\n');
            int fileCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                bool match = ignoreCase
                    ? lines[i].IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0
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
                Console.WriteLine(prefix + fileCount);
            }
        }
        return found > 0 ? 0 : 1;
    }
}
