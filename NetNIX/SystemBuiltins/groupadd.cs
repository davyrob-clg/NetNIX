using System;
using System.Linq;
using NetNIX.Scripting;

public static class GroupaddCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
        {
            Console.WriteLine("groupadd - create a new group");
            Console.WriteLine();
            Console.WriteLine("Usage: groupadd <groupname>");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  groupadd developers");
            Console.WriteLine("  groupadd staff");
            return args.Length == 0 ? 1 : 0;
        }

        string name = args[0];

        if (api.Uid != 0)
        {
            Console.WriteLine("groupadd: permission denied (must be root)");
            return 1;
        }

        try
        {
            api.CreateGroup(name);
            api.Save();
            Console.WriteLine($"groupadd: group '{name}' created");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"groupadd: {ex.Message}");
            return 1;
        }
    }
}
