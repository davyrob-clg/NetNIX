/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetNIX.Users;
using NetNIX.VFS;

//ns version
namespace NetNIX.Scripting;

/// <summary>
/// Compiles and executes plain-text C# scripts stored in the virtual file system.
///
/// Scripts must define a class with a static method:
///     static int Run(NixApi api, string[] args)
/// The class name does not matter — the runner finds the first matching method.
/// Return 0 for success, non-zero for error.
/// </summary>
public sealed class ScriptRunner
{
    private readonly VirtualFileSystem _fs;
    private readonly UserManager _userMgr;

    // Simple in-memory cache: source hash ? compiled assembly
    private readonly Dictionary<int, Assembly> _cache = [];

    // Search path for commands (in order)
    private static readonly string[] SearchDirs = ["/bin", "/sbin", "/usr/bin", "/usr/sbin", "/usr/local/bin", "/usr/local/sbin"];

    public ScriptRunner(VirtualFileSystem fs, UserManager userMgr)
    {
        _fs = fs;
        _userMgr = userMgr;
    }

    /// <summary>
    /// Attempts to find and run a script command.
    /// Returns true if a script was found (even if it failed), false if no script exists.
    /// </summary>
    public bool TryRunCommand(string command, List<string> args, UserRecord user, string cwd)
    {
        string? scriptPath = ResolveCommand(command, cwd);
        if (scriptPath == null)
            return false;

        var node = _fs.GetNode(scriptPath);
        if (node == null || node.IsDirectory)
            return false;

        if (!node.CanRead(user.Uid, user.Gid))
        {
            Console.WriteLine($"nsh: {command}: Permission denied");
            return true;
        }

        string source = Encoding.UTF8.GetString(_fs.ReadFile(scriptPath));
        RunSource(source, args.ToArray(), user, cwd, command);
        return true;
    }

    /// <summary>
    /// Compiles and runs an arbitrary .cs file path within the VFS.
    /// </summary>
    public void RunFile(string vfsPath, string[] args, UserRecord user, string cwd)
    {
        if (!_fs.IsFile(vfsPath))
        {
            Console.WriteLine($"run: {vfsPath}: No such file");
            return;
        }

        string source = Encoding.UTF8.GetString(_fs.ReadFile(vfsPath));
        RunSource(source, args, user, cwd, vfsPath);
    }

    // ?? Internal ???????????????????????????????????????????????????

    private string? ResolveCommand(string command, string cwd)
    {
        // If command contains a slash, treat as explicit path
        if (command.Contains('/'))
        {
            string resolved = VirtualFileSystem.ResolvePath(cwd, command);
            if (_fs.IsFile(resolved) && !IsShellScript(resolved)) return resolved;
            if (_fs.IsFile(resolved + ".cs")) return resolved + ".cs";
            return null;
        }

        // Check current working directory first
        string cwdPath = cwd.TrimEnd('/') + "/" + command;
        if (_fs.IsFile(cwdPath) && !IsShellScript(cwdPath)) return cwdPath;
        if (_fs.IsFile(cwdPath + ".cs")) return cwdPath + ".cs";

        // Search the PATH directories
        foreach (var dir in SearchDirs)
        {
            string candidate = dir + "/" + command + ".cs";
            if (_fs.IsFile(candidate))
                return candidate;

            string bare = dir + "/" + command;
            if (_fs.IsFile(bare) && !IsShellScript(bare))
                return bare;
        }

        return null;
    }

    /// <summary>
    /// Returns true if the path looks like a shell script rather than a C# source file.
    /// </summary>
    private bool IsShellScript(string vfsPath)
    {
        if (vfsPath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
            return true;

        // Peek at content — shell scripts start with # (comment or shebang)
        var node = _fs.GetNode(vfsPath);
        if (node?.Data is { Length: > 0 })
        {
            string first = Encoding.UTF8.GetString(node.Data).TrimStart();
            if (first.StartsWith('#'))
                return true;
        }

        return false;
    }

    // Search paths for libraries (in order)
    private static readonly string[] LibSearchDirs = ["/lib", "/usr/lib", "/usr/local/lib"];

    private void RunSource(string source, string[] args, UserRecord user, string cwd, string label)
    {
        // Preprocess #include directives — merge library sources into the script
        source = PreprocessIncludes(source, cwd, label);

        // Load per-script sandbox exceptions
        var exceptions = LoadSandboxExceptions(label);
        // Also match by bare filename (e.g. "nxconfig" for "/sbin/nxconfig.cs")
        string bareName = label.Contains('/') ? label.Substring(label.LastIndexOf('/') + 1) : label;
        if (bareName.EndsWith(".cs")) bareName = bareName[..^3];
        foreach (var ex in LoadSandboxExceptions(bareName))
            exceptions.Add(ex);

        // Security: reject scripts that attempt to bypass the sandbox
        if (!PassesSecurityScan(source, label, exceptions))
            return;

        int hash = source.GetHashCode();

        // Determine extra assemblies needed based on exceptions
        HashSet<string>? extraAssemblies = null;
        foreach (var exc in exceptions)
        {
            if (ExceptionAssemblies.TryGetValue(exc, out var asms))
            {
                extraAssemblies ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var asm in asms)
                    extraAssemblies.Add(asm);
            }
        }

        if (!_cache.TryGetValue(hash, out var assembly))
        {
            assembly = CompileExtended(source, label, extraAssemblies);
            if (assembly == null) return; // errors already printed
            _cache[hash] = assembly;
        }

        // Find the Run(NixApi, string[]) method
        MethodInfo? entry = null;
        foreach (var type in assembly.GetTypes())
        {
            entry = type.GetMethod("Run", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                [typeof(NixApi), typeof(string[])]);
            if (entry != null) break;
        }

        if (entry == null)
        {
            Console.WriteLine($"nsh: {label}: script has no static Run(NixApi, string[]) method");
            return;
        }

        var api = new NixApi(_fs, _userMgr, user.Uid, user.Gid, user.Username, cwd);

        try
        {
            entry.Invoke(null, [api, args]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            Console.ResetColor();
            Console.Error.WriteLine($"nsh: {label}: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            Console.ResetColor();
            Console.Error.WriteLine($"nsh: {label}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes #include directives in script source code.
    ///
    /// Supported forms:
    ///   #include &lt;libname&gt;       — searches /lib, /usr/lib, /usr/local/lib for libname.cs
    ///   #include "path/to/file.cs" — resolves relative to cwd or as absolute VFS path
    ///
    /// Includes are resolved recursively (libraries can include other libraries).
    /// Circular includes are detected and skipped.
    /// </summary>
    private string PreprocessIncludes(string source, string cwd, string label)
    {
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return ResolveIncludes(source, cwd, label, included);
    }

    private string ResolveIncludes(string source, string cwd, string label, HashSet<string> included)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var scriptBody = new StringBuilder();
        var libSources = new StringBuilder();

        foreach (var line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("#include"))
            {
                string? libSource = ResolveInclude(trimmed, cwd, label, included);
                if (libSource != null)
                    libSources.AppendLine(libSource);
                continue;
            }

            scriptBody.AppendLine(line);
        }

        // Merge library source with the script source
        if (libSources.Length > 0)
        {
            libSources.AppendLine();
            libSources.Append(scriptBody);
            return HoistUsings(libSources.ToString());
        }

        return scriptBody.ToString();
    }

    /// <summary>
    /// Extracts all 'using' directives from merged source and moves them
    /// to the top, deduplicated. This prevents CS1529 errors when library
    /// code with classes appears before the script's using statements.
    /// </summary>
    private static string HoistUsings(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var usings = new HashSet<string>(StringComparer.Ordinal);
        var body = new StringBuilder();

        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            // Match "using X.Y.Z;" but not "using (" or "using var"
            if (trimmed.StartsWith("using ") && trimmed.EndsWith(";") &&
                !trimmed.StartsWith("using (") && !trimmed.StartsWith("using var "))
            {
                usings.Add(trimmed);
            }
            else
            {
                body.AppendLine(line);
            }
        }

        var result = new StringBuilder();
        foreach (var u in usings.OrderBy(u => u))
            result.AppendLine(u);
        if (usings.Count > 0)
            result.AppendLine();
        result.Append(body);
        return result.ToString();
    }

    private string? ResolveInclude(string directive, string cwd, string label, HashSet<string> included)
    {
        // Parse:  #include <name>  or  #include "path"
        string rest = directive.Substring("#include".Length).Trim();

        string? vfsPath = null;

        if (rest.StartsWith('<') && rest.EndsWith('>'))
        {
            // Angle-bracket form: search library directories
            string libName = rest.Trim('<', '>').Trim();
            vfsPath = ResolveLibrary(libName);
        }
        else if (rest.StartsWith('"') && rest.EndsWith('"'))
        {
            // Quote form: resolve relative to cwd
            string path = rest.Trim('"').Trim();
            string resolved = VirtualFileSystem.ResolvePath(cwd, path);
            if (_fs.IsFile(resolved))
                vfsPath = resolved;
            else if (_fs.IsFile(resolved + ".cs"))
                vfsPath = resolved + ".cs";
        }

        if (vfsPath == null)
        {
            Console.WriteLine($"nsh: {label}: include not found: {rest}");
            return null;
        }

        // Prevent circular includes
        if (!included.Add(vfsPath))
            return null;

        string source = Encoding.UTF8.GetString(_fs.ReadFile(vfsPath));

        // Recursively resolve includes within the library
        return ResolveIncludes(source, VirtualFileSystem.GetParent(vfsPath), vfsPath, included);
    }

    /// <summary>
    /// Search library directories for a named library (e.g. "demoapilib" ? /lib/demoapilib.cs).
    /// </summary>
    private string? ResolveLibrary(string name)
    {
        foreach (var dir in LibSearchDirs)
        {
            string candidate = dir + "/" + name + ".cs";
            if (_fs.IsFile(candidate))
                return candidate;

            // Also try without extension
            string bare = dir + "/" + name;
            if (_fs.IsFile(bare))
                return bare;
        }
        return null;
    }

    // ?? Security ????????????????????????????????????????????????????

    /// <summary>
    /// The default content installed to /etc/sandbox.conf during first-run setup.
    /// Root (or sudo) users can edit this file to add or remove rules.
    /// Lines starting with # are comments.  Blank lines are ignored.
    /// 
    /// [blocked_usings]   — namespace prefixes checked against 'using' directives.
    ///                      "using System.IO;" is blocked if "System.IO" is listed.
    /// [blocked_tokens]   — literal strings searched for anywhere in the source.
    ///                      Catches short names like "File." after a using import.
    /// </summary>
    public const string DefaultSandboxConfig = """
        # /etc/sandbox.conf — NetNIX script sandbox configuration
        # Managed by root. Changes take effect on the next script run.
        #
        # [blocked_usings]  — blocks 'using' directives that start with these prefixes.
        # [blocked_tokens]  — blocks scripts containing these literal strings.
        #
        # To unblock something, comment it out with # or remove the line.
        # To add a new rule, add a line under the appropriate section.

        [blocked_usings]
        System.IO
        System.Diagnostics
        System.Net
        System.Reflection
        System.Runtime.Loader
        System.Runtime.InteropServices
        System.Security
        System.CodeDom

        [blocked_tokens]
        # Host filesystem — short names reachable after 'using System.IO'
        File.Create
        File.Open
        File.Read
        File.Write
        File.Copy
        File.Move
        File.Delete
        File.Exists
        File.AppendAll
        File.WriteAll
        File.ReadAll
        File.OpenRead
        File.OpenWrite
        File.OpenText
        File.CreateText
        Directory.Create
        Directory.Delete
        Directory.Exists
        Directory.Move
        Directory.GetFiles
        Directory.GetDirectories
        Directory.EnumerateFiles
        Directory.EnumerateDirectories
        DirectoryInfo(
        FileInfo(
        FileStream(
        StreamReader(
        StreamWriter(
        DriveInfo.
        Path.Combine
        Path.GetTempPath
        Path.GetFullPath
        # Process spawning
        Process.Start
        ProcessStartInfo(
        # Network (must use api.Net)
        HttpClient(
        WebClient(
        TcpClient(
        UdpClient(
        Socket(
        # Reflection / assembly loading
        Assembly.Load
        Assembly.UnsafeLoad
        Activator.CreateInstance
        GetField(
        GetProperty(
        GetMethod(
        BindingFlags.
        Type.GetType(
        AssemblyLoadContext
        # Unsafe / interop
        DllImport
        Marshal.
        # Environment manipulation
        Environment.Exit
        Environment.SetEnvironmentVariable
        Environment.CurrentDirectory
        Environment.GetFolderPath
        """;

    /// <summary>
    /// Loads and parses /etc/sandbox.conf from the VFS.
    /// Returns two lists: blocked using prefixes and blocked source tokens.
    /// </summary>
    private (List<string> blockedUsings, List<string> blockedTokens) LoadSandboxConfig()
    {
        var blockedUsings = new List<string>();
        var blockedTokens = new List<string>();

        const string configPath = "/etc/sandbox.conf";
        if (!_fs.IsFile(configPath))
            return (blockedUsings, blockedTokens);

        string content = Encoding.UTF8.GetString(_fs.ReadFile(configPath));
        string currentSection = "";

        foreach (var rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim().ToLowerInvariant();
                continue;
            }

            switch (currentSection)
            {
                case "blocked_usings":
                    blockedUsings.Add(line);
                    break;
                case "blocked_tokens":
                    blockedTokens.Add(line);
                    break;
            }
        }

        return (blockedUsings, blockedTokens);
    }

    /// <summary>
    /// Scans the preprocessed script source against the sandbox config.
    /// Checks 'using' directives against blocked namespace prefixes and
    /// scans the full source for blocked token strings.
    /// If <paramref name="exceptions"/> is provided, matching rules are skipped.
    /// Returns true if the source is safe, false if blocked.
    /// </summary>
    private bool PassesSecurityScan(string source, string label, HashSet<string>? exceptions = null)
    {
        var (blockedUsings, blockedTokens) = LoadSandboxConfig();

        // Extract using directives from source
        foreach (var rawLine in source.Split('\n'))
        {
            string line = rawLine.Trim().TrimEnd('\r');
            // Match "using X.Y.Z;" but not "using (" or "using var"
            if (line.StartsWith("using ") && line.EndsWith(";") &&
                !line.StartsWith("using (") && !line.StartsWith("using var "))
            {
                // Extract the namespace: "using System.IO;" -> "System.IO"
                string ns = line["using ".Length..^1].Trim();

                foreach (var blocked in blockedUsings)
                {
                    if (ns.Equals(blocked, StringComparison.Ordinal) ||
                        ns.StartsWith(blocked + ".", StringComparison.Ordinal))
                    {
                        // Check exceptions
                        if (exceptions != null && (exceptions.Contains(blocked) || exceptions.Contains(ns)))
                            continue;

                        Console.WriteLine($"nsh: {label}: blocked — 'using {ns}' is not permitted");
                        Console.WriteLine($"  Namespace '{blocked}' is blocked by /etc/sandbox.conf");
                        Console.WriteLine("  Scripts must use the NixApi for all file, network, and system operations.");
                        Console.WriteLine("  Root can edit /etc/sandbox.conf to modify sandbox rules.");
                        return false;
                    }
                }
            }
        }

        // Scan full source for blocked tokens
        foreach (var token in blockedTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
            {
                // Check exceptions
                if (exceptions != null && exceptions.Contains(token))
                    continue;

                Console.WriteLine($"nsh: {label}: blocked — use of '{token}' is not permitted");
                Console.WriteLine("  This pattern is blocked by /etc/sandbox.conf");
                Console.WriteLine("  Scripts must use the NixApi for all file, network, and system operations.");
                Console.WriteLine("  Root can edit /etc/sandbox.conf to modify sandbox rules.");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Loads /etc/sandbox.exceptions — lists per-script sandbox rule overrides.
    /// Format: &lt;script-name-or-path&gt; &lt;namespace-or-token&gt;
    /// </summary>
    private HashSet<string> LoadSandboxExceptions(string scriptNameOrPath)
    {
        var exceptions = new HashSet<string>(StringComparer.Ordinal);
        const string path = "/etc/sandbox.exceptions";
        if (!_fs.IsFile(path))
            return exceptions;

        string content = Encoding.UTF8.GetString(_fs.ReadFile(path));
        foreach (var rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            // Split on first whitespace
            int space = line.IndexOfAny([' ', '\t']);
            if (space <= 0) continue;

            string scriptKey = line[..space].Trim();
            string exception = line[(space + 1)..].Trim();

            // Match if script name or path matches
            if (scriptKey.Equals(scriptNameOrPath, StringComparison.OrdinalIgnoreCase) ||
                scriptKey.Equals("*", StringComparison.Ordinal) ||
                scriptNameOrPath.EndsWith("/" + scriptKey, StringComparison.OrdinalIgnoreCase) ||
                scriptNameOrPath.EndsWith("/" + scriptKey + ".cs", StringComparison.OrdinalIgnoreCase))
            {
                exceptions.Add(exception);
            }
        }

        return exceptions;
    }

    /// <summary>
    /// Default content for /etc/sandbox.exceptions.
    /// </summary>
    public const string DefaultSandboxExceptions = """
        # /etc/sandbox.exceptions — per-script sandbox overrides
        # Managed by root. Changes take effect on the next script/daemon run.
        #
        # Format:  <script-name>  <namespace-or-token>
        #
        # The script name can be a bare name (e.g. "httpd"), a full VFS path
        # (e.g. "/sbin/httpd.cs"), or "*" to apply to all scripts.
        #
        # Each line grants ONE exception to ONE script.

        # httpd — HTTP server daemon (uncomment to enable)
        # httpd  System.Net
        # httpd  HttpListener(

        # nxconfig — host environment configuration (reads/writes config file)
        nxconfig  System.IO
        nxconfig  File.Exists
        nxconfig  File.Read
        nxconfig  File.ReadAll
        nxconfig  File.Write
        nxconfig  File.WriteAll
        nxconfig  Path.Combine
        """;

    /// <summary>
    /// Additional assembly names that can be unlocked via sandbox exceptions.
    /// Only loaded for scripts that have matching exceptions.
    /// </summary>
    private static readonly Dictionary<string, string[]> ExceptionAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Net"] = ["System.Net.Primitives", "System.Net.HttpListener", "System.Net.Sockets", "System.Net.Http", "System.Private.Uri"],
        ["System.Net.Http"] = ["System.Net.Http", "System.Net.Primitives"],
        ["System.Net.Sockets"] = ["System.Net.Sockets", "System.Net.Primitives"],
        ["System.IO"] = ["System.IO.FileSystem", "System.IO.FileSystem.Primitives"],
        ["System.Diagnostics"] = ["System.Diagnostics.Process"],
    };

    /// <summary>
    /// Compile a script with sandbox exceptions applied. Used by DaemonManager.
    /// Preprocesses includes, runs security scan with exceptions, and compiles
    /// with extra assemblies as needed.
    /// Returns the compiled assembly, or null on failure.
    /// </summary>
    public Assembly? CompileWithExceptions(string source, string name, string vfsPath)
    {
        string cwd = VirtualFileSystem.GetParent(vfsPath);
        source = PreprocessIncludes(source, cwd, name);

        var exceptions = LoadSandboxExceptions(name);
        // Also check by full path
        foreach (var ex in LoadSandboxExceptions(vfsPath))
            exceptions.Add(ex);

        if (!PassesSecurityScan(source, name, exceptions))
            return null;

        // Determine extra assemblies needed based on exceptions
        var extraAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var exc in exceptions)
        {
            if (ExceptionAssemblies.TryGetValue(exc, out var asms))
            {
                foreach (var asm in asms)
                    extraAssemblies.Add(asm);
            }
        }

        return CompileExtended(source, name, extraAssemblies);
    }

    /// <summary>
    /// Compile with the standard allowlist plus additional assemblies.
    /// </summary>
    private static Assembly? CompileExtended(string source, string label, HashSet<string>? extraAssemblies = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new List<MetadataReference>();

        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var asmPath in trustedAssemblies)
        {
            string name = Path.GetFileNameWithoutExtension(asmPath);
            bool allowed = AllowedAssemblies.Contains(name) ||
                           (extraAssemblies != null && extraAssemblies.Contains(name));
            if (!allowed) continue;
            try { references.Add(MetadataReference.CreateFromFile(asmPath)); }
            catch { /* skip unavailable */ }
        }

        var selfLocation = typeof(ScriptRunner).Assembly.Location;
        if (!string.IsNullOrEmpty(selfLocation) && File.Exists(selfLocation))
            references.Add(MetadataReference.CreateFromFile(selfLocation));

        var compilation = CSharpCompilation.Create(
            $"NixScript_{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            Console.WriteLine($"nsh: {label}: compilation failed:");
            foreach (var diag in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                Console.WriteLine($"  {diag}");
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    // Allowlist of assembly names that scripts are permitted to reference.
    // This prevents user scripts from accessing the host filesystem, network,
    // process spawning, reflection, or other dangerous APIs directly.
    // All host interaction must go through the NixApi surface.
    private static readonly HashSet<string> AllowedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core runtime (primitive types, object, string, arrays, etc.)
        "System.Runtime",
        "System.Private.CoreLib",

        // Console I/O (Console.WriteLine, etc.)
        "System.Console",

        // Collections (List<T>, Dictionary<K,V>, HashSet<T>, etc.)
        "System.Collections",
        "System.Collections.Immutable",
        "System.Collections.Concurrent",

        // LINQ
        "System.Linq",
        "System.Linq.Expressions",

        // Text (StringBuilder, Encoding, Regex)
        "System.Text.RegularExpressions",
        "System.Text.Encoding.Extensions",

        // Math / numerics
        "System.Numerics.Vectors",
        "System.Runtime.Numerics",

        // Basic utilities
        "System.ComponentModel.Primitives",
        "System.ObjectModel",
        "System.Memory",
        "System.Buffers",
        "System.Threading",

        // Required for compilation plumbing
        "netstandard",
        "System.Runtime.Extensions",
        "System.Runtime.InteropServices",
    };

    private static Assembly? Compile(string source, string label)
    {
        return CompileExtended(source, label);
    }
}
