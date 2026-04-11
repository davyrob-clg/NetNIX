using System;
using NetNIX.Scripting;

public static class HostnameCommand
{
    public static int Run(NixApi api, string[] args)
    {
        Console.WriteLine("netnix");
        return 0;
    }
}
