using System;
using NetNIX.Scripting;

public static class FetchCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
        {
            Console.WriteLine("fetch - quick HTTP GET and print response");
            Console.WriteLine();
            Console.WriteLine("Usage: fetch <url>");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  fetch https://httpbin.org/get");
            Console.WriteLine("  fetch https://api.github.com");
            Console.WriteLine("  fetch https://example.com");
            return args.Length == 0 ? 1 : 0;
        }

        string url = args[0];
        string body = api.Net.Get(url);

        if (body == null)
        {
            Console.WriteLine($"fetch: failed to reach {url}");
            return 1;
        }

        Console.Write(body);
        if (!body.EndsWith('\n'))
            Console.WriteLine();

        return 0;
    }
}
