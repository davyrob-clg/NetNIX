using System;
using System.Linq;
using NetNIX.Scripting;

public static class UseraddCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        if (argList.Count == 0 || argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return argList.Count == 0 ? 1 : 0;
        }

        bool createHome = argList.Remove("-m") || argList.Remove("--create-home");
        string home = null;
        string shell = null;
        string password = null;
        var groups = new System.Collections.Generic.List<string>();

        for (int i = 0; i < argList.Count; i++)
        {
            if ((argList[i] == "-d" || argList[i] == "--home") && i + 1 < argList.Count)
            {
                home = argList[i + 1];
                argList.RemoveRange(i, 2); i--;
            }
            else if ((argList[i] == "-s" || argList[i] == "--shell") && i + 1 < argList.Count)
            {
                shell = argList[i + 1];
                argList.RemoveRange(i, 2); i--;
            }
            else if ((argList[i] == "-p" || argList[i] == "--password") && i + 1 < argList.Count)
            {
                password = argList[i + 1];
                argList.RemoveRange(i, 2); i--;
            }
            else if ((argList[i] == "-G" || argList[i] == "--groups") && i + 1 < argList.Count)
            {
                groups.AddRange(argList[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries));
                argList.RemoveRange(i, 2); i--;
            }
        }

        if (argList.Count == 0)
        {
            Console.WriteLine("useradd: missing username");
            return 1;
        }

        string username = argList[0];

        if (api.Uid != 0)
        {
            Console.WriteLine("useradd: permission denied (must be root)");
            return 1;
        }

        if (password == null)
        {
            password = username; // default password = username
            Console.WriteLine($"useradd: no password specified, defaulting to username");
        }

        try
        {
            api.CreateUser(username, password, home);

            if (shell != null)
                api.ModifyUser(username, newShell: shell);

            foreach (var g in groups)
            {
                try { api.AddUserToGroup(username, g); }
                catch (Exception ex) { Console.WriteLine($"useradd: warning: {ex.Message}"); }
            }

            api.Save();
            Console.WriteLine($"useradd: user '{username}' created");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"useradd: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("useradd - create a new user account");
        Console.WriteLine();
        Console.WriteLine("Usage: useradd [options] <username>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -m, --create-home     Create home directory (default)");
        Console.WriteLine("  -d, --home DIR        Set home directory path");
        Console.WriteLine("  -s, --shell SHELL     Set login shell");
        Console.WriteLine("  -p, --password PASS   Set password (default: username)");
        Console.WriteLine("  -G, --groups G1,G2    Add to supplementary groups");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  useradd alice");
        Console.WriteLine("  useradd -p secret -d /home/bob -s /bin/nsh bob");
        Console.WriteLine("  useradd -G developers,staff carol");
    }
}
