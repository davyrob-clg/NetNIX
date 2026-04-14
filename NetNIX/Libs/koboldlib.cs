using System;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NetNIX.Scripting;

/// <summary>
/// koboldlib — KoboldCpp REST API client library for NetNIX scripts.
///
/// Include in your scripts with:
///     #include &lt;koboldlib&gt;
///
/// Provides methods to communicate with a KoboldCpp (or compatible)
/// text-generation API endpoint.
///
/// Settings (managed via settingslib):
///     App name: "kobold"
///
///     endpoint       Base URL of the KoboldCpp API (default: http://localhost:5001)
///     max_context    Max context length in tokens   (default: 4096)
///     max_length     Max tokens to generate         (default: 256)
///     temperature    Sampling temperature 0.0-2.0   (default: 0.7)
///     top_p          Top-p (nucleus) sampling        (default: 0.9)
///     top_k          Top-k sampling                  (default: 40)
///     rep_penalty    Repetition penalty              (default: 1.1)
///     timeout        Request timeout in seconds      (default: 120)
///     system_prompt  System prompt prepended to chat (default: built-in)
///
/// Usage:
///     #include &lt;koboldlib&gt;
///     #include &lt;settingslib&gt;
///
///     var ai = new KoboldApi(api);
///     string reply = ai.Generate("Tell me a joke.");
///     Console.WriteLine(reply);
///
///     // Chat with memory
///     var chat = new KoboldChat(api);
///     chat.Say("Hello!");
///     string response = chat.LastReply;
/// </summary>
public class KoboldApi
{
    private readonly NixApi _api;
    internal const string AppName = "kobold";

    // Default values — used when no settings file exists
    internal static readonly Dictionary<string, string> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["endpoint"]      = "http://localhost:5001",
        ["max_context"]   = "4096",
        ["max_length"]    = "256",
        ["temperature"]   = "0.7",
        ["top_p"]         = "0.9",
        ["top_k"]         = "40",
        ["rep_penalty"]   = "1.1",
        ["timeout"]       = "120",
        ["system_prompt"] = "You are a helpful AI assistant running inside NetNIX, a UNIX-like environment. Be concise and helpful.",
    };

    // Properties
    public string Endpoint { get; set; }
    public int MaxContext { get; set; }
    public int MaxLength { get; set; }
    public double Temperature { get; set; }
    public double TopP { get; set; }
    public int TopK { get; set; }
    public double RepPenalty { get; set; }
    public int Timeout { get; set; }
    public string SystemPrompt { get; set; }

    public KoboldApi(NixApi api)
    {
        _api = api;
        EnsureSettings();
        LoadSettings();
    }

    /// <summary>
    /// Ensure default settings files exist. Creates system-wide defaults
    /// in /etc/opt/kobold.conf and per-user defaults in ~/.config/kobold.conf
    /// if they don't already exist. Called automatically on construction.
    /// </summary>
    public void EnsureSettings()
    {
        // System-wide defaults (only if root and file doesn't exist)
        string sysPath = "/etc/opt/" + AppName + ".conf";
        if (_api.Uid == 0 && !_api.IsFile(sysPath))
        {
            foreach (var kv in Defaults)
                Settings.SetSystem(_api, AppName, kv.Key, kv.Value);
        }

        // Per-user defaults (if file doesn't exist)
        string home = _api.Uid == 0 ? "/root" : "/home/" + _api.Username;
        string userPath = home + "/.config/" + AppName + ".conf";
        if (!_api.IsFile(userPath))
        {
            foreach (var kv in Defaults)
                Settings.Set(_api, AppName, kv.Key, kv.Value);
        }
    }

    /// <summary>
    /// Reload all settings from the settingslib store.
    /// </summary>
    public void LoadSettings()
    {
        Endpoint    = GetSetting("endpoint",      Defaults["endpoint"]);
        MaxContext   = int.Parse(GetSetting("max_context", Defaults["max_context"]));
        MaxLength    = int.Parse(GetSetting("max_length",  Defaults["max_length"]));
        Temperature  = double.Parse(GetSetting("temperature", Defaults["temperature"]), CultureInfo.InvariantCulture);
        TopP         = double.Parse(GetSetting("top_p",       Defaults["top_p"]), CultureInfo.InvariantCulture);
        TopK         = int.Parse(GetSetting("top_k",          Defaults["top_k"]));
        RepPenalty   = double.Parse(GetSetting("rep_penalty",  Defaults["rep_penalty"]), CultureInfo.InvariantCulture);
        Timeout      = int.Parse(GetSetting("timeout",        Defaults["timeout"]));
        SystemPrompt = GetSetting("system_prompt", Defaults["system_prompt"]);
    }

    /// <summary>
    /// Send a prompt to the /api/v1/generate endpoint and return the generated text.
    /// Returns null on failure.
    /// </summary>
    public string Generate(string prompt)
    {
        string url = Endpoint.TrimEnd('/') + "/api/v1/generate";
        string json = BuildGenerateBody(prompt);
        string response = _api.Net.Post(url, json, "application/json");
        if (response == null) return null;
        return ParseGenerateResponse(response);
    }

    /// <summary>
    /// Send a raw prompt directly to the API with no system prompt,
    /// no chat formatting, and no role markers. Returns the raw
    /// generated text, or null on failure.
    /// </summary>
    public string GenerateRaw(string prompt)
    {
        string url = Endpoint.TrimEnd('/') + "/api/v1/generate";
        string json = BuildGenerateBody(prompt);
        string response = _api.Net.Post(url, json, "application/json");
        if (response == null) return null;
        return ParseGenerateResponse(response);
    }

    /// <summary>
    /// Check if the KoboldCpp API is reachable.
    /// </summary>
    public bool IsAvailable()
    {
        string url = Endpoint.TrimEnd('/') + "/api/v1/model";
        return _api.Net.IsReachable(url);
    }

    /// <summary>
    /// Get the currently loaded model name from KoboldCpp.
    /// Returns null on failure.
    /// </summary>
    public string GetModelName()
    {
        string url = Endpoint.TrimEnd('/') + "/api/v1/model";
        string response = _api.Net.Get(url);
        if (response == null) return null;
        return ParseJsonValue(response, "result");
    }

    /// <summary>
    /// Get the max context length reported by the KoboldCpp server.
    /// Returns -1 on failure.
    /// </summary>
    public int GetServerMaxContext()
    {
        string url = Endpoint.TrimEnd('/') + "/api/v1/config/max_context_length";
        string response = _api.Net.Get(url);
        if (response == null) return -1;
        string val = ParseJsonValue(response, "value");
        return val != null && int.TryParse(val, out int v) ? v : -1;
    }

    // --- Settings helper ---

    private string GetSetting(string key, string defaultValue)
    {
        // Per-user overrides system-wide
        string val = Settings.Get(_api, AppName, key);
        if (val != null) return val;
        val = Settings.GetSystem(_api, AppName, key);
        if (val != null) return val;
        return defaultValue;
    }

    // --- JSON construction (manual, no System.Text.Json in sandbox) ---

    private string BuildGenerateBody(string prompt)
    {
        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append(JsonPair("prompt", prompt)); sb.Append(',');
        sb.Append(JsonNum("max_context_length", MaxContext)); sb.Append(',');
        sb.Append(JsonNum("max_length", MaxLength)); sb.Append(',');
        sb.Append(JsonFloat("temperature", Temperature)); sb.Append(',');
        sb.Append(JsonFloat("top_p", TopP)); sb.Append(',');
        sb.Append(JsonNum("top_k", TopK)); sb.Append(',');
        sb.Append(JsonFloat("rep_pen", RepPenalty));
        sb.Append('}');
        return sb.ToString();
    }

    private static string JsonPair(string key, string value)
    {
        return "\"" + EscapeJson(key) + "\":\"" + EscapeJson(value) + "\"";
    }

    private static string JsonNum(string key, int value)
    {
        return "\"" + EscapeJson(key) + "\":" + value;
    }

    private static string JsonFloat(string key, double value)
    {
        return "\"" + EscapeJson(key) + "\":" + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string EscapeJson(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    // --- Minimal JSON parsing ---

    internal static string ParseGenerateResponse(string json)
    {
        // KoboldCpp returns: {"results":[{"text":"..."}]}
        int resultsIdx = json.IndexOf("\"results\"");
        if (resultsIdx < 0) return null;

        int textIdx = json.IndexOf("\"text\"", resultsIdx);
        if (textIdx < 0) return null;

        return ExtractJsonStringValue(json, textIdx);
    }

    internal static string ParseJsonValue(string json, string key)
    {
        string search = "\"" + key + "\"";
        int idx = json.IndexOf(search);
        if (idx < 0) return null;
        return ExtractJsonStringValue(json, idx);
    }

    private static string ExtractJsonStringValue(string json, int keyStart)
    {
        // Find the colon after the key
        int colon = json.IndexOf(':', keyStart);
        if (colon < 0) return null;

        // Skip whitespace after colon
        int i = colon + 1;
        while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
        if (i >= json.Length) return null;

        // If it's a string value
        if (json[i] == '"')
        {
            i++; // skip opening quote
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(next); break;
                    }
                    i += 2;
                }
                else if (json[i] == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(json[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        // If it's a number/bool/null — read until delimiter
        var numSb = new StringBuilder();
        while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']' && !char.IsWhiteSpace(json[i]))
        {
            numSb.Append(json[i]);
            i++;
        }
        return numSb.ToString();
    }
}

/// <summary>
/// High-level chat interface for KoboldCpp.
/// Maintains conversation history and formats prompts automatically.
/// </summary>
public class KoboldChat
{
    private readonly KoboldApi _kobold;
    private readonly List<(string role, string text)> _history = new();

    /// <summary>
    /// The last reply from the AI, or null if no conversation has happened.
    /// </summary>
    public string LastReply { get; private set; }

    /// <summary>
    /// The full conversation history as (role, text) pairs.
    /// </summary>
    public IReadOnlyList<(string role, string text)> History => _history;

    public KoboldChat(NixApi api)
    {
        _kobold = new KoboldApi(api);
    }

    public KoboldChat(KoboldApi kobold)
    {
        _kobold = kobold;
    }

    /// <summary>
    /// Send a user message and get the AI response.
    /// Returns the AI's reply, or null on failure.
    /// </summary>
    public string Say(string message)
    {
        _history.Add(("user", message));

        string prompt = BuildPrompt();
        string reply = _kobold.Generate(prompt);

        if (reply != null)
        {
            reply = reply.Trim();
            // Clean up: stop at next "User:" or "### User" if the model runs on
            int cutoff = FindRoleCutoff(reply);
            if (cutoff > 0)
                reply = reply.Substring(0, cutoff).Trim();

            _history.Add(("assistant", reply));
            LastReply = reply;
        }
        else
        {
            LastReply = null;
        }

        return LastReply;
    }

    /// <summary>
    /// Clear the conversation history.
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        LastReply = null;
    }

    /// <summary>
    /// Get the number of exchanges (user messages) so far.
    /// </summary>
    public int TurnCount => (_history.Count + 1) / 2;

    private string BuildPrompt()
    {
        var sb = new StringBuilder();

        // System prompt
        if (!string.IsNullOrEmpty(_kobold.SystemPrompt))
        {
            sb.AppendLine("### System:");
            sb.AppendLine(_kobold.SystemPrompt);
            sb.AppendLine();
        }

        // Conversation history
        foreach (var (role, text) in _history)
        {
            if (role == "user")
            {
                sb.AppendLine("### User:");
                sb.AppendLine(text);
            }
            else
            {
                sb.AppendLine("### Assistant:");
                sb.AppendLine(text);
            }
            sb.AppendLine();
        }

        // Prompt the model to respond
        sb.Append("### Assistant:\n");
        return sb.ToString();
    }

    private static int FindRoleCutoff(string text)
    {
        string[] markers = { "\n### User:", "\n### System:", "\nUser:", "\nSystem:" };
        int earliest = -1;
        foreach (var marker in markers)
        {
            int idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (earliest < 0 || idx < earliest))
                earliest = idx;
        }
        return earliest;
    }
}
