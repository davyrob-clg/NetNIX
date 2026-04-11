using System;
using NetNIX.Scripting;

public static class TeeCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = new System.Collections.Generic.List<string>(args);
        bool append = argList.Remove("-a");

        if (argList.Count == 0)
        {
            Console.WriteLine("tee: usage: tee [-a] <file>");
            return 1;
        }

        string file = argList[0];
        Console.WriteLine("Enter text (type '.' on a line by itself to finish):");

        var sb = new System.Text.StringBuilder();
        while (true)
        {
            string line = Console.ReadLine();
            if (line == null || line == ".") break;
            Console.WriteLine(line);
            sb.AppendLine(line);
        }

        if (append)
            api.AppendText(file, sb.ToString());
        else
            api.WriteText(file, sb.ToString());

        api.Save();
        return 0;
    }
}
