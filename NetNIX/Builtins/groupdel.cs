using System;
using System.Linq;
using NetNIX.Scripting;

public static class GroupdelCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
        {
            Console.WriteLine("groupdel - delete a group");
            Console.WriteLine();
            Console.WriteLine("Usage: groupdel <groupname>");
            Console.WriteLine();
            Console.WriteLine("  Cannot delete a group that is still a user's primary group.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  groupdel developers");
            return args.Length == 0 ? 1 : 0;
        }

        string name = args[0];

        if (api.Uid != 0)
        {
            Console.WriteLine("groupdel: permission denied (must be root)");
            return 1;
        }

        try
        {
            api.DeleteGroup(name);
            api.Save();
            Console.WriteLine($"groupdel: group '{name}' deleted");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"groupdel: {ex.Message}");
            return 1;
        }
    }
}
