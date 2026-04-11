using System;
using NetNIX.Scripting;

public static class PwdCommand
{
    public static int Run(NixApi api, string[] args)
    {
        Console.WriteLine(api.Cwd);
        return 0;
    }
}
