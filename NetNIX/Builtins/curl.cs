using System;
using System.Linq;
using NetNIX.Scripting;

public static class CurlCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        if (argList.Count == 0 || argList.Remove("-h") || argList.Remove("--help"))
        {
            PrintUsage();
            return argList.Count == 0 ? 1 : 0;
        }

        // Parse options
        bool silent = argList.Remove("-s") || argList.Remove("--silent");
        bool includeHeaders = argList.Remove("-i") || argList.Remove("--include");
        bool headOnly = argList.Remove("-I") || argList.Remove("--head");
        string method = "GET";
        string body = null;
        string contentType = "application/json";
        string outputFile = null;
        var headers = new System.Collections.Generic.List<(string, string)>();

        for (int i = 0; i < argList.Count; i++)
        {
            if ((argList[i] == "-X" || argList[i] == "--request") && i + 1 < argList.Count)
            {
                method = argList[i + 1].ToUpper();
                argList.RemoveRange(i, 2);
                i--;
            }
            else if ((argList[i] == "-d" || argList[i] == "--data") && i + 1 < argList.Count)
            {
                body = argList[i + 1];
                if (method == "GET") method = "POST";
                argList.RemoveRange(i, 2);
                i--;
            }
            else if ((argList[i] == "-H" || argList[i] == "--header") && i + 1 < argList.Count)
            {
                string hdr = argList[i + 1];
                int colon = hdr.IndexOf(':');
                if (colon > 0)
                    headers.Add((hdr.Substring(0, colon).Trim(), hdr.Substring(colon + 1).Trim()));
                argList.RemoveRange(i, 2);
                i--;
            }
            else if ((argList[i] == "-o" || argList[i] == "--output") && i + 1 < argList.Count)
            {
                outputFile = argList[i + 1];
                argList.RemoveRange(i, 2);
                i--;
            }
            else if (argList[i] == "--content-type" && i + 1 < argList.Count)
            {
                contentType = argList[i + 1];
                argList.RemoveRange(i, 2);
                i--;
            }
        }

        if (argList.Count == 0)
        {
            Console.WriteLine("curl: no URL specified");
            return 1;
        }

        string url = argList[0];

        if (headOnly)
        {
            int status = api.Net.Head(url);
            if (status < 0)
            {
                if (!silent) Console.WriteLine($"curl: could not connect to {url}");
                return 1;
            }
            Console.WriteLine($"HTTP {status}");
            return status >= 200 && status < 400 ? 0 : 1;
        }

        var response = api.Net.Request(method, url, body, contentType, headers.ToArray());

        if (!response.IsSuccess && !silent)
        {
            Console.WriteLine($"curl: HTTP {response.StatusCode} {response.StatusText}");
        }

        if (includeHeaders || !silent && !response.IsSuccess)
        {
            Console.WriteLine($"HTTP/{response.StatusCode} {response.StatusText}");
            foreach (var (name, value) in response.Headers)
                Console.WriteLine($"{name}: {value}");
            Console.WriteLine();
        }

        if (outputFile != null)
        {
            api.WriteText(outputFile, response.Body);
            api.Save();
            if (!silent)
                Console.WriteLine($"curl: saved to {outputFile}");
        }
        else
        {
            Console.Write(response.Body);
            if (!response.Body.EndsWith('\n'))
                Console.WriteLine();
        }

        return response.IsSuccess ? 0 : 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("curl - transfer data from a URL");
        Console.WriteLine();
        Console.WriteLine("Usage: curl [options] <url>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -X METHOD       HTTP method (GET, POST, PUT, DELETE)");
        Console.WriteLine("  -d DATA         Request body (implies POST)");
        Console.WriteLine("  -H \"Key: Val\"   Add request header");
        Console.WriteLine("  --content-type  Set content type (default: application/json)");
        Console.WriteLine("  -o FILE         Save response to VFS file");
        Console.WriteLine("  -i              Include response headers");
        Console.WriteLine("  -I              HEAD request only (show status)");
        Console.WriteLine("  -s              Silent mode (suppress errors)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  curl https://httpbin.org/get");
        Console.WriteLine("  curl -X POST -d '{\"key\":\"val\"}' https://httpbin.org/post");
        Console.WriteLine("  curl -o page.html https://example.com");
        Console.WriteLine("  curl -I https://example.com");
    }
}
