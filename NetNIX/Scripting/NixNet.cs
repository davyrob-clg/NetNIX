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
