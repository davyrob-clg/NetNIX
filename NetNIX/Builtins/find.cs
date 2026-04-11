using System;
using System.Linq;
using NetNIX.Scripting;

public static class FindCommand
{
    public static int Run(NixApi api, string[] args)
    {
        string startDir = ".";
        string namePattern = null;
        string typeFilter = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-name" && i + 1 < args.Length)
                namePattern = args[++i];
            else if (args[i] == "-type" && i + 1 < args.Length)
                typeFilter = args[++i];
            else if (!args[i].StartsWith("-"))
                startDir = args[i];
        }

        string resolved = api.ResolvePath(startDir);
        var allPaths = api.GetAllPaths();

        foreach (var path in allPaths)
        {
            if (!path.StartsWith(resolved)) continue;

            string name = api.NodeName(path);
            bool isDir = api.IsDirAbsolute(path);

            if (typeFilter == "f" && isDir) continue;
            if (typeFilter == "d" && !isDir) continue;

            if (namePattern != null)
            {
                if (namePattern.StartsWith("*") && namePattern.EndsWith("*"))
                {
                    if (!name.Contains(namePattern.Trim('*'), StringComparison.OrdinalIgnoreCase)) continue;
                }
                else if (namePattern.StartsWith("*"))
                {
                    if (!name.EndsWith(namePattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase)) continue;
                }
                else if (namePattern.EndsWith("*"))
                {
                    if (!name.StartsWith(namePattern.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)) continue;
                }
                else
                {
                    if (!name.Equals(namePattern, StringComparison.OrdinalIgnoreCase)) continue;
                }
            }

            Console.WriteLine(path);
        }
        return 0;
    }
}
