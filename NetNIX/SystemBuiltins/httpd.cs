using System;
using System.Net;
using System.Text;
using NetNIX.Scripting;

/// <summary>
/// httpd — a simple HTTP server daemon for NetNIX.
///
/// Serves files from the VFS over HTTP. Listens on localhost.
/// Requires sandbox exceptions for System.Net (configured by root).
///
/// Start:  daemon start httpd [port]
/// Stop:   daemon stop httpd
///
/// Sandbox exceptions required in /etc/sandbox.exceptions:
///   httpd  System.Net
///   httpd  HttpListener(
/// </summary>
public static class HttpdDaemon
{
    /// <summary>
    /// Daemon entry point — runs on a background thread.
    /// </summary>
    public static int Daemon(NixApi api, string[] args)
    {
        int port = 8080;
        string webRoot = "/var/www";

        // Parse args
        if (args.Length > 0 && int.TryParse(args[0], out int p))
            port = p;
        if (args.Length > 1)
            webRoot = args[1];

        // Ensure web root exists
        if (!api.IsDirectory(webRoot))
        {
            api.CreateDirWithParents(webRoot);
        }

        // Create a default index.html if none exists
        string indexPath = webRoot.TrimEnd('/') + "/index.html";
        if (!api.IsFile(indexPath))
        {
            api.WriteText(indexPath, """
                <!DOCTYPE html>
                <html>
                <head><title>NetNIX httpd</title></head>
                <body>
                    <h1>Welcome to NetNIX httpd</h1>
                    <p>This page is served from the virtual filesystem.</p>
                    <p>Web root: /var/www</p>
                </body>
                </html>
                """);
            api.Save();
        }

        HttpListener listener;
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"httpd: failed to start on port {port}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"httpd: listening on http://localhost:{port}/  (web root: {webRoot})");

        var token = api.DaemonToken;

        try
        {
            while (!token.IsCancellationRequested)
            {
                // Wait for a request with cancellation support
                var getCtxTask = listener.GetContextAsync();
                try
                {
                    getCtxTask.Wait(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (!getCtxTask.IsCompletedSuccessfully)
                    continue;

                var ctx = getCtxTask.Result;
                HandleRequest(api, ctx, webRoot);
            }
        }
        finally
        {
            try { listener.Stop(); } catch { }
            try { listener.Close(); } catch { }
            Console.WriteLine("httpd: stopped");
        }

        return 0;
    }

    private static void HandleRequest(NixApi api, HttpListenerContext ctx, string webRoot)
    {
        string urlPath = ctx.Request.Url?.AbsolutePath ?? "/";
        if (urlPath == "/") urlPath = "/index.html";

        // Sanitize: no .. traversal
        urlPath = urlPath.Replace("..", "").Replace("\\", "/");
        string vfsPath = webRoot.TrimEnd('/') + urlPath;

        byte[] body;
        string contentType;
        int statusCode;

        if (api.IsFile(vfsPath))
        {
            body = api.ReadBytes(vfsPath);
            contentType = GuessContentType(vfsPath);
            statusCode = 200;
        }
        else if (api.IsDirectory(vfsPath))
        {
            // Directory listing
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><body>");
            sb.AppendLine($"<h1>Index of {urlPath}</h1><ul>");
            if (urlPath != "/")
                sb.AppendLine("<li><a href=\"..\">..</a></li>");
            foreach (var entry in api.ListDirectory(vfsPath))
            {
                string name = api.NodeName(entry);
                bool isDir = api.IsDirectory(entry);
                sb.AppendLine($"<li><a href=\"{urlPath.TrimEnd('/')}/{name}\">{name}{(isDir ? "/" : "")}</a></li>");
            }
            sb.AppendLine("</ul></body></html>");
            body = Encoding.UTF8.GetBytes(sb.ToString());
            contentType = "text/html";
            statusCode = 200;
        }
        else
        {
            body = Encoding.UTF8.GetBytes("404 Not Found");
            contentType = "text/plain";
            statusCode = 404;
        }

        try
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = body.Length;
            ctx.Response.OutputStream.Write(body, 0, body.Length);
            ctx.Response.Close();
        }
        catch { /* client may have disconnected */ }
    }

    private static string GuessContentType(string path)
    {
        if (path.EndsWith(".html") || path.EndsWith(".htm")) return "text/html";
        if (path.EndsWith(".css")) return "text/css";
        if (path.EndsWith(".js")) return "application/javascript";
        if (path.EndsWith(".json")) return "application/json";
        if (path.EndsWith(".txt") || path.EndsWith(".cs")) return "text/plain";
        if (path.EndsWith(".png")) return "image/png";
        if (path.EndsWith(".jpg") || path.EndsWith(".jpeg")) return "image/jpeg";
        if (path.EndsWith(".gif")) return "image/gif";
        if (path.EndsWith(".svg")) return "image/svg+xml";
        if (path.EndsWith(".xml")) return "application/xml";
        return "application/octet-stream";
    }

    /// <summary>
    /// Standard Run method for non-daemon usage (help text only).
    /// </summary>
    public static int Run(NixApi api, string[] args)
    {
        Console.WriteLine("httpd — NetNIX HTTP server daemon");
        Console.WriteLine();
        Console.WriteLine("This is a daemon script. Start it with:");
        Console.WriteLine("  daemon start httpd [port] [web-root]");
        Console.WriteLine();
        Console.WriteLine("Defaults:");
        Console.WriteLine("  port     8080");
        Console.WriteLine("  web-root /var/www");
        Console.WriteLine();
        Console.WriteLine("Stop it with:");
        Console.WriteLine("  daemon stop httpd");
        Console.WriteLine();
        Console.WriteLine("Sandbox exceptions required (add to /etc/sandbox.exceptions):");
        Console.WriteLine("  httpd  System.Net");
        Console.WriteLine("  httpd  HttpListener(");
        Console.WriteLine();
        Console.WriteLine("See 'man daemon' for more information.");
        return 0;
    }
}
