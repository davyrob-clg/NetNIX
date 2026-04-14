using System;
using System;
using System.Collections.Generic;
using System.Globalization;
using NetNIX.Scripting;

#include <settingslib>
#include <koboldlib>

/// <summary>
/// kobold — Interactive AI chat client for KoboldCpp.
///
/// Connects to a KoboldCpp (or compatible) text-generation API and
/// provides an interactive chat session in the terminal.
///
/// Usage:
///     kobold                  Start interactive chat
///     kobold --status         Check API connection and model info
///     kobold --config         Show current settings
///     kobold --set key value  Change a setting
///     kobold -m "message"     Send a single message and print the reply
///     kobold --help           Show help
/// </summary>
public static class KoboldCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = new List<string>(args);

        // --help
        if (argList.Count == 0 && !Console.IsInputRedirected)
        {
            // No args, launch interactive mode (fall through)
        }
        else if (argList.Contains("-h") || argList.Contains("--help"))
        {
            PrintUsage();
            return 0;
        }

        // --status
        if (argList.Contains("--status"))
        {
            return CmdStatus(api);
        }

        // --config
        if (argList.Contains("--config"))
        {
            return CmdConfig(api);
        }

        // --set key value
        int setIdx = argList.IndexOf("--set");
        if (setIdx >= 0)
        {
            if (setIdx + 2 >= argList.Count)
            {
                Console.WriteLine("kobold: --set requires a key and value");
                Console.WriteLine("Usage: kobold --set endpoint http://192.168.1.10:5001");
                return 1;
            }
            return CmdSet(api, argList[setIdx + 1], argList[setIdx + 2]);
        }

        // --reset
        if (argList.Contains("--reset"))
        {
            return CmdReset(api);
        }

        // --raw "prompt" — raw generation with no system prompt
        int rawIdx = argList.IndexOf("--raw");
        if (rawIdx >= 0)
        {
            if (rawIdx + 1 >= argList.Count)
            {
                Console.WriteLine("kobold: --raw requires a prompt string");
                return 1;
            }
            return CmdRaw(api, argList[rawIdx + 1]);
        }

        // -m "message" — single-shot mode
        int msgIdx = argList.IndexOf("-m");
        if (msgIdx < 0) msgIdx = argList.IndexOf("--message");
        if (msgIdx >= 0)
        {
            if (msgIdx + 1 >= argList.Count)
            {
                Console.WriteLine("kobold: -m requires a message");
                return 1;
            }
            return CmdSingleMessage(api, argList[msgIdx + 1]);
        }

        // Default: interactive chat
        return CmdChat(api);
    }

    // --- Interactive chat ---

    private static int CmdChat(NixApi api)
    {
        var kobold = new KoboldApi(api);

        // Check connection first
        Console.Write("Connecting to ");
        Console.Write(kobold.Endpoint);
        Console.Write("...");

        if (!kobold.IsAvailable())
        {
            Console.WriteLine(" FAILED");
            Console.WriteLine();
            Console.WriteLine("Could not reach the KoboldCpp API.");
            Console.WriteLine("Make sure KoboldCpp is running and the endpoint is correct.");
            Console.WriteLine();
            Console.WriteLine("  Current endpoint: " + kobold.Endpoint);
            Console.WriteLine("  Change with:      kobold --set endpoint http://HOST:PORT");
            return 1;
        }

        string model = kobold.GetModelName() ?? "unknown";
        Console.WriteLine(" OK");
        Console.WriteLine("Model: " + model);
        Console.WriteLine();
        Console.WriteLine("Type your messages below. Commands:");
        Console.WriteLine("  /quit, /exit     End the chat");
        Console.WriteLine("  /clear           Clear conversation history");
        Console.WriteLine("  /model           Show current model name");
        Console.WriteLine("  /config          Show settings");
        Console.WriteLine("  /set key value   Change a setting for this session");
        Console.WriteLine("  /save            Save conversation to a file");
        Console.WriteLine("  /help            Show these commands");
        Console.WriteLine();

        var chat = new KoboldChat(kobold);

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("You> ");
            Console.ResetColor();

            string input = Console.ReadLine();
            if (input == null) break; // EOF

            input = input.Trim();
            if (input.Length == 0) continue;

            // Slash commands
            if (input.StartsWith("/"))
            {
                string cmd = input.ToLowerInvariant();
                if (cmd == "/quit" || cmd == "/exit" || cmd == "/q")
                    break;

                if (cmd == "/clear")
                {
                    chat.Clear();
                    Console.WriteLine("Conversation cleared.");
                    Console.WriteLine();
                    continue;
                }

                if (cmd == "/model")
                {
                    string m = kobold.GetModelName() ?? "unknown";
                    Console.WriteLine("Model: " + m);
                    Console.WriteLine();
                    continue;
                }

                if (cmd == "/config")
                {
                    PrintSettings(kobold);
                    Console.WriteLine();
                    continue;
                }

                if (cmd == "/help")
                {
                    Console.WriteLine("  /quit, /exit     End the chat");
                    Console.WriteLine("  /clear           Clear conversation history");
                    Console.WriteLine("  /model           Show current model name");
                    Console.WriteLine("  /config          Show settings");
                    Console.WriteLine("  /set key value   Change a setting for this session");
                    Console.WriteLine("  /save            Save conversation to a file");
                    Console.WriteLine("  /help            Show these commands");
                    Console.WriteLine();
                    continue;
                }

                if (cmd.StartsWith("/set "))
                {
                    HandleInlineSet(kobold, input.Substring(5).Trim());
                    continue;
                }

                if (cmd.StartsWith("/save"))
                {
                    HandleSave(api, chat, input);
                    continue;
                }

                Console.WriteLine("Unknown command: " + input);
                Console.WriteLine("Type /help for available commands.");
                Console.WriteLine();
                continue;
            }

            // Send message
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("AI> ");
            Console.ResetColor();

            string reply = chat.Say(input);

            if (reply == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Error: no response from API. Check connection with /model]");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(reply);
            }
            Console.WriteLine();
        }

        Console.WriteLine("Goodbye.");
        return 0;
    }

    // --- Single message ---

    private static int CmdSingleMessage(NixApi api, string message)
    {
        var kobold = new KoboldApi(api);

        if (!kobold.IsAvailable())
        {
            Console.Error.WriteLine("kobold: cannot reach API at " + kobold.Endpoint);
            return 1;
        }

        var chat = new KoboldChat(kobold);
        string reply = chat.Say(message);

        if (reply == null)
        {
            Console.Error.WriteLine("kobold: no response from API");
            return 1;
        }

        Console.WriteLine(reply);
        return 0;
    }

    // --- Raw generation ---

    private static int CmdRaw(NixApi api, string prompt)
    {
        var kobold = new KoboldApi(api);

        if (!kobold.IsAvailable())
        {
            Console.Error.WriteLine("kobold: cannot reach API at " + kobold.Endpoint);
            return 1;
        }

        string reply = kobold.GenerateRaw(prompt);

        if (reply == null)
        {
            Console.Error.WriteLine("kobold: no response from API");
            return 1;
        }

        Console.WriteLine(reply);
        return 0;
    }

    // --- Reset ---

    private static int CmdReset(NixApi api)
    {
        // Remove per-user settings
        foreach (var key in KoboldApi.Defaults.Keys)
            Settings.Remove(api, KoboldApi.AppName, key);

        // Re-create with defaults
        var kobold = new KoboldApi(api);
        Console.WriteLine("kobold: settings reset to defaults");
        Console.WriteLine();
        PrintSettingsDetailed(api, kobold);
        return 0;
    }

    // --- Status ---

    private static int CmdStatus(NixApi api)
    {
        var kobold = new KoboldApi(api);

        Console.WriteLine("KoboldCpp Status");
        Console.WriteLine("  Endpoint:  " + kobold.Endpoint);
        Console.Write("  Reachable: ");

        if (!kobold.IsAvailable())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("NO");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("YES");
        Console.ResetColor();

        string model = kobold.GetModelName() ?? "unknown";
        int ctx = kobold.GetServerMaxContext();

        Console.WriteLine("  Model:     " + model);
        if (ctx > 0)
            Console.WriteLine("  Max ctx:   " + ctx);

        return 0;
    }

    // --- Config ---

    private static int CmdConfig(NixApi api)
    {
        var kobold = new KoboldApi(api);

        string home = api.Uid == 0 ? "/root" : "/home/" + api.Username;
        string userPath = home + "/.config/kobold.conf";
        string sysPath = "/etc/opt/kobold.conf";

        Console.WriteLine("KoboldCpp Settings");
        Console.WriteLine();

        Console.WriteLine("  Per-user file:   " + userPath + (api.IsFile(userPath) ? "" : " (not created)"));
        Console.WriteLine("  System-wide file: " + sysPath + (api.IsFile(sysPath) ? "" : " (not created)"));
        Console.WriteLine();

        PrintSettingsDetailed(api, kobold);

        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  kobold --set <key> <value>  Change a setting (per-user)");
        Console.WriteLine("  kobold --reset             Reset all settings to defaults");
        Console.WriteLine();
        Console.WriteLine("Configurable keys:");
        Console.WriteLine("  endpoint       API base URL");
        Console.WriteLine("  max_context    Max context length (tokens)");
        Console.WriteLine("  max_length     Max generation length (tokens)");
        Console.WriteLine("  temperature    Sampling temperature (0.0-2.0)");
        Console.WriteLine("  top_p          Top-p nucleus sampling (0.0-1.0)");
        Console.WriteLine("  top_k          Top-k sampling");
        Console.WriteLine("  rep_penalty    Repetition penalty (1.0 = none)");
        Console.WriteLine("  timeout        Request timeout (seconds)");
        Console.WriteLine("  system_prompt  System prompt for chat");

        return 0;
    }

    // --- Set ---

    private static int CmdSet(NixApi api, string key, string value)
    {
        var validKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "endpoint", "max_context", "max_length", "temperature",
            "top_p", "top_k", "rep_penalty", "timeout", "system_prompt"
        };

        if (!validKeys.Contains(key))
        {
            Console.WriteLine("kobold: unknown setting '" + key + "'");
            Console.WriteLine("Run 'kobold --config' to see valid keys.");
            return 1;
        }

        Settings.Set(api, "kobold", key, value);
        Console.WriteLine("kobold: " + key + " = " + value);
        return 0;
    }

    // --- Helpers ---

    private static void PrintSettings(KoboldApi kobold)
    {
        Console.WriteLine("  endpoint       = " + kobold.Endpoint);
        Console.WriteLine("  max_context    = " + kobold.MaxContext);
        Console.WriteLine("  max_length     = " + kobold.MaxLength);
        Console.WriteLine("  temperature    = " + kobold.Temperature.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("  top_p          = " + kobold.TopP.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("  top_k          = " + kobold.TopK);
        Console.WriteLine("  rep_penalty    = " + kobold.RepPenalty.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("  timeout        = " + kobold.Timeout);
        Console.WriteLine("  system_prompt  = " + Truncate(kobold.SystemPrompt, 60));
    }

    private static void PrintSettingsDetailed(NixApi api, KoboldApi kobold)
    {
        Console.WriteLine("  Key              Value                                    Source");
        Console.WriteLine("  ???????????????  ????????????????????????????????????????  ??????????");
        PrintSettingRow(api, "endpoint",      kobold.Endpoint);
        PrintSettingRow(api, "max_context",   kobold.MaxContext.ToString());
        PrintSettingRow(api, "max_length",    kobold.MaxLength.ToString());
        PrintSettingRow(api, "temperature",   kobold.Temperature.ToString(CultureInfo.InvariantCulture));
        PrintSettingRow(api, "top_p",         kobold.TopP.ToString(CultureInfo.InvariantCulture));
        PrintSettingRow(api, "top_k",         kobold.TopK.ToString());
        PrintSettingRow(api, "rep_penalty",   kobold.RepPenalty.ToString(CultureInfo.InvariantCulture));
        PrintSettingRow(api, "timeout",       kobold.Timeout.ToString());
        PrintSettingRow(api, "system_prompt", Truncate(kobold.SystemPrompt, 35));
    }

    private static void PrintSettingRow(NixApi api, string key, string displayValue)
    {
        string source = SettingSource(api, key);
        Console.WriteLine("  " + key.PadRight(15) + displayValue.PadRight(41) + source);
    }

    private static string SettingSource(NixApi api, string key)
    {
        string userVal = Settings.Get(api, KoboldApi.AppName, key);
        string sysVal = Settings.GetSystem(api, KoboldApi.AppName, key);

        if (userVal != null && sysVal != null)
            return "user (overrides system)";
        if (userVal != null)
            return "user";
        if (sysVal != null)
            return "system";
        return "default";
    }

    private static void HandleInlineSet(KoboldApi kobold, string args)
    {
        int space = args.IndexOf(' ');
        if (space <= 0)
        {
            Console.WriteLine("Usage: /set <key> <value>");
            return;
        }

        string key = args.Substring(0, space).Trim().ToLowerInvariant();
        string value = args.Substring(space + 1).Trim();

        switch (key)
        {
            case "max_context": if (int.TryParse(value, out int mc)) kobold.MaxContext = mc; break;
            case "max_length":  if (int.TryParse(value, out int ml)) kobold.MaxLength = ml; break;
            case "temperature": if (double.TryParse(value, out double t)) kobold.Temperature = t; break;
            case "top_p":       if (double.TryParse(value, out double tp)) kobold.TopP = tp; break;
            case "top_k":       if (int.TryParse(value, out int tk)) kobold.TopK = tk; break;
            case "rep_penalty": if (double.TryParse(value, out double rp)) kobold.RepPenalty = rp; break;
            case "timeout":     if (int.TryParse(value, out int to)) kobold.Timeout = to; break;
            case "system_prompt": kobold.SystemPrompt = value; break;
            case "endpoint":    kobold.Endpoint = value; break;
            default:
                Console.WriteLine("Unknown setting: " + key);
                return;
        }

        Console.WriteLine("  " + key + " = " + value + " (session only — use 'kobold --set' to persist)");
    }

    private static void HandleSave(NixApi api, KoboldChat chat, string input)
    {
        string path = null;
        if (input.Length > 5)
            path = input.Substring(5).Trim();

        if (string.IsNullOrEmpty(path))
            path = "~/kobold-chat.txt";

        // Resolve ~
        if (path.StartsWith("~/"))
            path = (api.Uid == 0 ? "/root" : "/home/" + api.Username) + path.Substring(1);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# KoboldCpp Chat Log — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        foreach (var (role, text) in chat.History)
        {
            sb.AppendLine(role == "user" ? "You> " + text : "AI> " + text);
            sb.AppendLine();
        }

        try
        {
            api.WriteText(path, sb.ToString());
            Console.WriteLine("Conversation saved to " + path);
        }
        catch (Exception ex)
        {
            Console.WriteLine("kobold: failed to save: " + ex.Message);
        }
        Console.WriteLine();
    }

    private static string Truncate(string s, int maxLen)
    {
        if (s == null) return "";
        if (s.Length <= maxLen) return s;
        return s.Substring(0, maxLen) + "...";
    }

    private static void PrintUsage()
    {
        Console.WriteLine("kobold — AI chat client for KoboldCpp");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  kobold                    Start interactive chat");
        Console.WriteLine("  kobold -m \"message\"       Send a single message");
        Console.WriteLine("  kobold --raw \"prompt\"     Send a raw prompt (no system prompt)");
        Console.WriteLine("  kobold --status           Check API connection");
        Console.WriteLine("  kobold --config           Show current settings with sources");
        Console.WriteLine("  kobold --set key value    Change a setting (per-user)");
        Console.WriteLine("  kobold --reset            Reset all settings to defaults");
        Console.WriteLine("  kobold --help             Show this help");
        Console.WriteLine();
        Console.WriteLine("Settings (persisted per-user via settingslib):");
        Console.WriteLine("  endpoint       API base URL          (default: http://localhost:5001)");
        Console.WriteLine("  max_context    Max context tokens    (default: 4096)");
        Console.WriteLine("  max_length     Max generation tokens (default: 256)");
        Console.WriteLine("  temperature    Sampling temperature  (default: 0.7)");
        Console.WriteLine("  top_p          Nucleus sampling      (default: 0.9)");
        Console.WriteLine("  top_k          Top-k sampling        (default: 40)");
        Console.WriteLine("  rep_penalty    Repetition penalty    (default: 1.1)");
        Console.WriteLine("  timeout        Timeout in seconds    (default: 120)");
        Console.WriteLine("  system_prompt  System prompt for AI  (default: built-in)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  kobold --set endpoint http://192.168.1.50:5001");
        Console.WriteLine("  kobold --set temperature 0.9");
        Console.WriteLine("  kobold -m \"What is the meaning of life?\"");
        Console.WriteLine("  kobold --raw \"Once upon a time\"");
        Console.WriteLine("  kobold --config");
        Console.WriteLine("  kobold --reset");
        Console.WriteLine("  kobold");
        Console.WriteLine();
        Console.WriteLine("Interactive commands:");
        Console.WriteLine("  /quit, /exit     End the chat");
        Console.WriteLine("  /clear           Clear conversation history");
        Console.WriteLine("  /model           Show current model name");
        Console.WriteLine("  /config          Show settings");
        Console.WriteLine("  /set key value   Change a setting for this session");
        Console.WriteLine("  /save [path]     Save conversation to a file");
        Console.WriteLine("  /help            Show commands");
        Console.WriteLine();
        Console.WriteLine("On first use, default settings are installed to:");
        Console.WriteLine("  ~/.config/kobold.conf      (per-user)");
        Console.WriteLine("  /etc/opt/kobold.conf       (system-wide, if root)");
        Console.WriteLine();
        Console.WriteLine("See also: man kobold, man koboldlib");
    }
}
