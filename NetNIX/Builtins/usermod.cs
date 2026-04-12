using System;
using System.Linq;
using NetNIX.Scripting;

public static class UsermodCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        if (argList.Count == 0 || argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return argList.Count == 0 ? 1 : 0;
        }

        bool lockUser = argList.Remove("-L") || argList.Remove("--lock");
        bool unlockUser = argList.Remove("-U") || argList.Remove("--unlock");
        string home = null;
        string shell = null;
        string addGroups = null;
        string removeGroup = null;

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
            else if ((argList[i] == "-aG" || argList[i] == "--append-group") && i + 1 < argList.Count)
            {
                addGroups = argList[i + 1];
                argList.RemoveRange(i, 2); i--;
            }
            else if ((argList[i] == "-rG" || argList[i] == "--remove-group") && i + 1 < argList.Count)
            {
                removeGroup = argList[i + 1];
                argList.RemoveRange(i, 2); i--;
            }
        }

        if (argList.Count == 0)
        {
            Console.WriteLine("usermod: missing username");
            return 1;
        }

        string username = argList[0];

        if (api.Uid != 0)
        {
            Console.WriteLine("usermod: permission denied (must be root)");
            return 1;
        }

        try
        {
            if (home != null || shell != null)
            {
                api.ModifyUser(username, home, shell);
                if (home != null) Console.WriteLine($"usermod: home directory set to {home}");
                if (shell != null) Console.WriteLine($"usermod: shell set to {shell}");
            }

            if (lockUser)
            {
                api.LockUser(username);
                Console.WriteLine($"usermod: user '{username}' locked");
            }

            if (unlockUser)
            {
                api.UnlockUser(username);
                Console.WriteLine($"usermod: user '{username}' unlocked");
            }

            if (addGroups != null)
            {
                foreach (var g in addGroups.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        api.AddUserToGroup(username, g);
                        Console.WriteLine($"usermod: added '{username}' to group '{g}'");
                    }
                    catch (Exception ex) { Console.WriteLine($"usermod: {ex.Message}"); }
                }
            }

            if (removeGroup != null)
            {
                foreach (var g in removeGroup.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        api.RemoveUserFromGroup(username, g);
                        Console.WriteLine($"usermod: removed '{username}' from group '{g}'");
                    }
                    catch (Exception ex) { Console.WriteLine($"usermod: {ex.Message}"); }
                }
            }

            api.Save();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"usermod: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("usermod - modify a user account");
        Console.WriteLine();
        Console.WriteLine("Usage: usermod [options] <username>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -d, --home DIR             Change home directory");
        Console.WriteLine("  -s, --shell SHELL          Change login shell");
        Console.WriteLine("  -L, --lock                 Lock user account");
        Console.WriteLine("  -U, --unlock               Unlock user account");
        Console.WriteLine("  -aG, --append-group G1,G2  Add to groups");
        Console.WriteLine("  -rG, --remove-group G1,G2  Remove from groups");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  usermod -d /home/newhome alice");
        Console.WriteLine("  usermod -s /bin/nsh bob");
        Console.WriteLine("  usermod -L carol");
        Console.WriteLine("  usermod -aG developers,staff alice");
        Console.WriteLine("  usermod -rG staff bob");
    }
}
