using System;
using System.Collections.Generic;
using NetNIX.Scripting;

/// <summary>
/// netlib — Networking utility library for NetNIX scripts.
///
/// Include in your scripts with:
///     #include &lt;netlib&gt;
///
/// Provides helper methods for common HTTP patterns:
/// JSON APIs, status checks, multi-URL fetching, and response parsing.
/// </summary>
public static class NetLib
{
    /// <summary>
    /// Fetch a URL and return the body, or a fallback string on failure.
    /// </summary>
    public static string GetOrDefault(NixApi api, string url, string fallback = "")
    {
        return api.Net.Get(url) ?? fallback;
    }

    /// <summary>
    /// Check if a URL returns a success status code.
    /// </summary>
    public static bool Ping(NixApi api, string url)
    {
        return api.Net.IsReachable(url);
    }

    /// <summary>
    /// Fetch multiple URLs and return results as an array of (url, body) tuples.
    /// Failed requests have a null body.
    /// </summary>
    public static (string url, string body)[] FetchAll(NixApi api, params string[] urls)
    {
        var results = new List<(string, string)>();
        foreach (var url in urls)
        {
            string body = api.Net.Get(url);
            results.Add((url, body));
        }
        return results.ToArray();
    }

    /// <summary>
    /// POST JSON to a URL and return the response body.
    /// </summary>
    public static string PostJson(NixApi api, string url, string json)
    {
        return api.Net.Post(url, json, "application/json") ?? "";
    }

    /// <summary>
    /// Download a URL to a VFS path and report the result.
    /// Returns a status message string.
    /// </summary>
    public static string DownloadWithStatus(NixApi api, string url, string path)
    {
        bool ok = api.Download(url, path);
        if (!ok) return $"FAILED: {url}";
        int size = api.GetSize(path);
        return $"OK: {url} -> {path} ({size} bytes)";
    }

    /// <summary>
    /// Extract a simple value from a JSON-like string by key.
    /// This is a basic pattern match, not a full JSON parser.
    /// Looks for "key": "value" or "key": number patterns.
    /// </summary>
    public static string JsonValue(string json, string key)
    {
        string pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return null;

        int colonIdx = json.IndexOf(':', idx + pattern.Length);
        if (colonIdx < 0) return null;

        // Skip whitespace after colon
        int start = colonIdx + 1;
        while (start < json.Length && json[start] == ' ') start++;

        if (start >= json.Length) return null;

        if (json[start] == '"')
        {
            // String value
            int end = json.IndexOf('"', start + 1);
            if (end < 0) return null;
            return json.Substring(start + 1, end - start - 1);
        }
        else
        {
            // Number/bool/null value — read until comma, brace, or bracket
            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
                end++;
            return json.Substring(start, end - start).Trim();
        }
    }

    /// <summary>
    /// Get HTTP response headers as a formatted string.
    /// </summary>
    public static string FormatHeaders(NixHttpResponse response)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"HTTP {response.StatusCode} {response.StatusText}");
        foreach (var (name, value) in response.Headers)
            sb.AppendLine($"{name}: {value}");
        return sb.ToString();
    }
}
