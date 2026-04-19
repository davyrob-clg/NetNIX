using System;
using System.Linq;
using NetNIX.Scripting;

public static class UserdelCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        if (argList.Count == 0 || argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return argList.Count == 0 ? 1 : 0;
        }

        bool removeHome = argList.Remove("-r") || argList.Remove("--remove");

        if (argList.Count == 0)
        {
            Console.WriteLine("userdel: missing username");
            return 1;
        }

        string username = argList[0];

        if (api.Uid != 0)
        {
            Console.WriteLine("userdel: permission denied (must be root)");
            return 1;
        }

        if (username == "root")
        {
            Console.WriteLine("userdel: cannot delete root user");
            return 1;
        }

        try
        {
            api.DeleteUser(username, removeHome);
            api.Save();
            Console.WriteLine($"userdel: user '{username}' deleted{(removeHome ? " (home removed)" : "")}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"userdel: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("userdel - delete a user account");
        Console.WriteLine();
        Console.WriteLine("Usage: userdel [options] <username>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -r, --remove    Remove user's home directory");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  userdel alice");
        Console.WriteLine("  userdel -r bob");
    }
}
