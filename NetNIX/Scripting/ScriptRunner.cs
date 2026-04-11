using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetNIX.Users;
using NetNIX.VFS;

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
    private static readonly string[] SearchDirs = ["/bin", "/usr/bin", "/usr/local/bin"];

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

        int hash = source.GetHashCode();

        if (!_cache.TryGetValue(hash, out var assembly))
        {
            assembly = Compile(source, label);
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
            Console.WriteLine($"nsh: {label}: {ex.InnerException.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"nsh: {label}: {ex.Message}");
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

    private static Assembly? Compile(string source, string label)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        // Gather references from the current runtime
        var references = new List<MetadataReference>();

        // Core runtime assemblies
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var asmPath in trustedAssemblies)
        {
            try { references.Add(MetadataReference.CreateFromFile(asmPath)); }
            catch { /* skip unavailable */ }
        }

        // Also reference the NetNIX assembly itself so scripts can use NixApi
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
            {
                Console.WriteLine($"  {diag}");
            }
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }
}
