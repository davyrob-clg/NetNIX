using System;
using NetNIX.Scripting;

public static class EnvCommand
{
    public static int Run(NixApi api, string[] args)
    {
        Console.WriteLine($"USER={api.Username}");
        Console.WriteLine($"UID={api.Uid}");
        Console.WriteLine($"GID={api.Gid}");
        Console.WriteLine($"HOME={api.ResolvePath("~")}");
        Console.WriteLine($"PWD={api.Cwd}");
        Console.WriteLine($"SHELL=/bin/nsh");
        Console.WriteLine($"HOSTNAME=netnix");
        Console.WriteLine($"PATH=/bin:/usr/bin:/usr/local/bin");
        Console.WriteLine($"TERM=netnix-256color");
        return 0;
    }
}
