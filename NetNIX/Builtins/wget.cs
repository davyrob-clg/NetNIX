using System;
using NetNIX.Scripting;

public static class WgetCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
        {
            Console.WriteLine("wget - download a file from a URL into the VFS");
            Console.WriteLine();
            Console.WriteLine("Usage: wget <url> [output-file]");
            Console.WriteLine();
            Console.WriteLine("  If no output file is given, the filename is derived");
            Console.WriteLine("  from the URL. Downloads are saved to the current directory.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  wget https://example.com/file.txt");
            Console.WriteLine("  wget https://example.com/data.json mydata.json");
            Console.WriteLine("  wget https://example.com/ index.html");
            return args.Length == 0 ? 1 : 0;
        }

        string url = args[0];
        string filename;

        if (args.Length > 1)
        {
            filename = args[1];
        }
        else
        {
            // Derive filename from URL
            string path = url;
            int queryIdx = path.IndexOf('?');
            if (queryIdx >= 0) path = path.Substring(0, queryIdx);
            int lastSlash = path.TrimEnd('/').LastIndexOf('/');
            filename = lastSlash >= 0 ? path.Substring(lastSlash + 1) : "download";
            if (string.IsNullOrWhiteSpace(filename)) filename = "index.html";
        }

        Console.WriteLine($"wget: downloading {url}");
        Console.WriteLine($"wget: saving to {filename}");

        bool ok = api.Download(url, filename);
        if (!ok)
        {
            Console.WriteLine("wget: download failed");
            return 1;
        }

        int size = api.GetSize(filename);
        Console.WriteLine($"wget: saved {size} bytes to {filename}");
        api.Save();
        return 0;
    }
}
