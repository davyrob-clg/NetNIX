using System;
using NetNIX.Scripting;

public static class DateCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length > 0 && args[0] == "-u")
            Console.WriteLine(DateTime.UtcNow.ToString("ddd MMM dd HH:mm:ss UTC yyyy"));
        else if (args.Length > 0 && args[0] == "-I")
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd"));
        else if (args.Length > 0 && args[0] == "+%s")
            Console.WriteLine(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        else
            Console.WriteLine(DateTime.Now.ToString("ddd MMM dd HH:mm:ss zzz yyyy"));
        return 0;
    }
}
