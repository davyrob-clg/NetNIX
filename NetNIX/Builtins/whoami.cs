using System;
using NetNIX.Scripting;

public static class WhoamiCommand
{
    public static int Run(NixApi api, string[] args)
    {
        Console.WriteLine(api.Username);
        return 0;
    }
}
