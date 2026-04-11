using System;
using NetNIX.Scripting;

public static class FalseCommand
{
    public static int Run(NixApi api, string[] args)
    {
        return 1;
    }
}
