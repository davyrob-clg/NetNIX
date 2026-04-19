using System;
using System.Linq;
using NetNIX.Scripting;

public static class MyCommand
{
    public static int Run(NixApi api, string[] args)
    {
        // Your code here
        Console.WriteLine("Hello World!");
        return 0;
    }
}
