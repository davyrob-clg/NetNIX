using System;
using NetNIX.Scripting;

public static class WcCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("wc: missing operand");
            return 1;
        }

        var argList = new System.Collections.Generic.List<string>(args);
        bool showLines = argList.Remove("-l");
        bool showWords = argList.Remove("-w");
        bool showBytes = argList.Remove("-c");
        if (!showLines && !showWords && !showBytes)
        {
            showLines = showWords = showBytes = true;
        }

        int totalLines = 0, totalWords = 0, totalBytes = 0;
        foreach (var file in argList)
        {
            if (!api.IsFile(file))
            {
                Console.WriteLine($"wc: {file}: No such file");
                continue;
            }
            string text = api.ReadText(file);
            int lines = text.Split('\n').Length;
            int words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int bytes = api.GetSize(file);
            totalLines += lines;
            totalWords += words;
            totalBytes += bytes;

            string result = "";
            if (showLines) result += $"  {lines,6}";
            if (showWords) result += $"  {words,6}";
            if (showBytes) result += $"  {bytes,6}";
            Console.WriteLine($"{result}  {file}");
        }

        if (argList.Count > 1)
        {
            string result = "";
            if (showLines) result += $"  {totalLines,6}";
            if (showWords) result += $"  {totalWords,6}";
            if (showBytes) result += $"  {totalBytes,6}";
            Console.WriteLine($"{result}  total");
        }
        return 0;
    }
}
