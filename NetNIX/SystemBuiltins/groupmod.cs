using System;
using System.Linq;
using NetNIX.Scripting;

public static class GroupmodCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        if (argList.Count == 0 || argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return argList.Count == 0 ? 1 : 0;
        }

        string newName = null;

        for (int i = 0; i < argList.Count; i++)
        {
            if ((argList[i] == "-n" || argList[i] == "--new-name") && i + 1 < argList.Count)
            {
                newName = argList[i + 1];
                argList.RemoveRange(i, 2); i--;
            }
        }

        if (argList.Count == 0)
        {
            Console.WriteLine("groupmod: missing group name");
            return 1;
        }

        string groupName = argList[0];

        if (api.Uid != 0)
        {
            Console.WriteLine("groupmod: permission denied (must be root)");
            return 1;
        }

        try
        {
            if (newName != null)
            {
                api.RenameGroup(groupName, newName);
                Console.WriteLine($"groupmod: group '{groupName}' renamed to '{newName}'");
            }
            else
            {
                Console.WriteLine("groupmod: no changes specified");
                return 1;
            }

            api.Save();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"groupmod: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("groupmod - modify a group");
        Console.WriteLine();
        Console.WriteLine("Usage: groupmod [options] <groupname>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -n, --new-name NAME   Rename the group");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  groupmod -n engineering developers");
    }
}
