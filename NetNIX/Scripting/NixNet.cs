/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
using System.Net;
using System.Text;

namespace NetNIX.Scripting;

/// <summary>
/// Networking API surface exposed to user scripts via api.Net.
/// Provides synchronous HTTP methods that bridge the host network
/// into the NetNIX scripting environment.
/// </summary>
public sealed class NixNet
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    // ?? HTTP GET ???????????????????????????????????????????????????

    /// <summary>
    /// Perform an HTTP GET request. Returns the response body as a string.
    /// Returns null on failure.
    /// </summary>
    public string? Get(string url)
    {
        try
        {
            using var response = _http.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"net: GET {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Perform an HTTP GET request. Returns the response body as a byte array.
    /// Returns null on failure.
    /// </summary>
    public byte[]? GetBytes(string url)
    {
        try
        {
            using var response = _http.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"net: GET {url}: {ex.Message}");
            return null;
        }
    }

    // ?? HTTP POST ??????????????????????????????????????????????????

    /// <summary>
    /// Perform an HTTP POST with a string body. Returns the response body as a string.
    /// </summary>
    public string? Post(string url, string body, string contentType = "application/json")
    {
        try
        {
            var content = new StringContent(body, Encoding.UTF8, contentType);
            using var response = _http.PostAsync(url, content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"net: POST {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Perform an HTTP POST with form data (key=value pairs).
    /// Returns the response body as a string.
    /// </summary>
    public string? PostForm(string url, params (string key, string value)[] fields)
    {
        try
        {
            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var (key, value) in fields)
                pairs.Add(new KeyValuePair<string, string>(key, value));

            var content = new FormUrlEncodedContent(pairs);
            using var response = _http.PostAsync(url, content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"net: POST {url}: {ex.Message}");
            return null;
        }
    }

    // ?? HTTP PUT / DELETE / PATCH ???????????????????????????????????

    /// <summary>
    /// Perform an HTTP PUT with a string body. Returns the response body.
    /// </summary>
    public string? Put(string url, string body, string contentType = "application/json")
    {
        try
        {
            var content = new StringContent(body, Encoding.UTF8, contentType);
            using var response = _http.PutAsync(url, content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"net: PUT {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Perform an HTTP DELETE. Returns the response body.
    /// </summary>
    public string? Delete(string url)
    {
        try
        {
            using var response = _http.DeleteAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"net: DELETE {url}: {ex.Message}");
            return null;
        }
    }

    // ?? Advanced request ???????????????????????????????????????????

    /// <summary>
    /// Perform a custom HTTP request with full control over method and headers.
    /// Returns a NixHttpResponse with status, headers, and body.
    /// </summary>
    public NixHttpResponse Request(string method, string url, string? body = null,
        string contentType = "application/json", params (string name, string value)[] headers)
    {
        try
        {
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            foreach (var (name, value) in headers)
                request.Headers.TryAddWithoutValidation(name, value);

            if (body != null)
                request.Content = new StringContent(body, Encoding.UTF8, contentType);

            using var response = _http.SendAsync(request).GetAwaiter().GetResult();

            var respHeaders = new List<(string name, string value)>();
            foreach (var h in response.Headers)
                respHeaders.Add((h.Key, string.Join(", ", h.Value)));
            foreach (var h in response.Content.Headers)
                respHeaders.Add((h.Key, string.Join(", ", h.Value)));

            return new NixHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? "",
                IsSuccess = response.IsSuccessStatusCode,
                Body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(),
                Headers = respHeaders.ToArray(),
            };
        }
        catch (Exception ex)
        {
            return new NixHttpResponse
            {
                StatusCode = 0,
                StatusText = ex.Message,
                IsSuccess = false,
                Body = "",
                Headers = [],
            };
        }
    }

    // ?? HEAD / status check ????????????????????????????????????????

    /// <summary>
    /// Perform an HTTP HEAD request. Returns the status code, or -1 on failure.
    /// </summary>
    public int Head(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = _http.SendAsync(request).GetAwaiter().GetResult();
            return (int)response.StatusCode;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Check if a URL is reachable (returns 2xx status).
    /// </summary>
    public bool IsReachable(string url)
    {
        int code = Head(url);
        return code >= 200 && code < 300;
    }

    // ?? POST with custom timeout ?????????????????????????????????????

    /// <summary>
    /// Perform an HTTP POST with a custom timeout (in seconds).
    /// Creates a dedicated HttpClient for the request so the default
    /// timeout is not affected. Use for long-running API calls.
    /// </summary>
    public string? PostWithTimeout(string url, string body, string contentType, int timeoutSeconds)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            var content = new StringContent(body, Encoding.UTF8, contentType);
            using var response = client.PostAsync(url, content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"net: POST {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Perform an HTTP POST with a custom timeout and custom headers.
    /// Creates a dedicated HttpClient so the default timeout is not affected.
    /// </summary>
    public string? PostWithTimeout(string url, string body, string contentType, int timeoutSeconds,
        params (string name, string value)[] headers)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType)
            };
            foreach (var (name, value) in headers)
                request.Headers.TryAddWithoutValidation(name, value);

            using var response = client.SendAsync(request).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"net: POST {url}: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Represents a full HTTP response from NixNet.Request().
/// </summary>
public sealed class NixHttpResponse
{
    public int StatusCode { get; init; }
    public string StatusText { get; init; } = "";
    public bool IsSuccess { get; init; }
    public string Body { get; init; } = "";
    public (string name, string value)[] Headers { get; init; } = [];

    /// <summary>Get a specific header value, or null if not found.</summary>
    public string? GetHeader(string name)
    {
        foreach (var (n, v) in Headers)
        {
            if (n.Equals(name, StringComparison.OrdinalIgnoreCase))
                return v;
        }
        return null;
    }
}
