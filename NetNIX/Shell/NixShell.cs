using System.Text;
using NetNIX.Scripting;
using NetNIX.Users;
using NetNIX.VFS;

namespace NetNIX.Shell;

/// <summary>
/// Interactive UNIX-like shell (nsh — NetNIX Shell).
/// </summary>
public sealed class NixShell
{
    private readonly VirtualFileSystem _fs;
    private readonly UserManager _userMgr;
    private readonly ScriptRunner _scriptRunner;
    private UserRecord _currentUser;
    private string _cwd;
    private bool _running = true;

    public NixShell(VirtualFileSystem fs, UserManager userMgr, ScriptRunner scriptRunner, UserRecord user)
    {
        _fs = fs;
        _userMgr = userMgr;
        _scriptRunner = scriptRunner;
        _currentUser = user;
        _cwd = user.HomeDirectory;

        if (!_fs.Exists(_cwd))
            _cwd = "/";
    }

    public void Run()
    {
        // Source the user's startup script if it exists
        RunStartupScript();

        while (_running)
        {
            string prompt = _currentUser.Uid == 0 ? "#" : "$";
            Console.Write($"{_currentUser.Username}@netnix:{_cwd}{prompt} ");
            string? input = Console.ReadLine();
            if (input == null) break;

            input = input.Trim();
            if (input.Length == 0) continue;

            try
            {
                Execute(input);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"nsh: {ex.Message}");
            }
        }
    }

    // ?? Command dispatcher ?????????????????????????????????????????

    private void Execute(string input)
    {
        var tokens = Tokenize(input);
        if (tokens.Count == 0) return;

        string cmd = tokens[0];
        var args = tokens.Skip(1).ToList();

        // Check for output redirection  (cmd > file  or  cmd >> file)
        string? redirectFile = null;
        bool appendMode = false;
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == ">>" && i + 1 < args.Count)
            {
                redirectFile = args[i + 1];
                appendMode = true;
                args.RemoveRange(i, 2);
                break;
            }
            if (args[i] == ">" && i + 1 < args.Count)
            {
                redirectFile = args[i + 1];
                appendMode = false;
                args.RemoveRange(i, 2);
                break;
            }
        }

        // Capture output if redirecting
        TextWriter originalOut = Console.Out;
        StringWriter? capture = null;
        if (redirectFile != null)
        {
            capture = new StringWriter();
            Console.SetOut(capture);
        }

        try
        {
            switch (cmd)
            {
                case "help": CmdHelp(); break;
                case "man": CmdMan(args); break;
                case "cd": CmdCd(args); break;
                case "edit": CmdEdit(args); break;
                case "write": CmdWrite(args); break;
                case "chmod": CmdChmod(args); break;
                case "chown": CmdChown(args); break;
                case "adduser": CmdAddUser(args); break;
                case "deluser": CmdDelUser(args); break;
                case "passwd": CmdPasswd(args); break;
                case "su": CmdSu(args); break;
                case "sudo": CmdSudo(args); break;
                case "users": CmdUsers(); break;
                case "groups": CmdGroups(); break;
                case "stat": CmdStat(args); break;
                case "tree": CmdTree(args); break;
                case "run": CmdRun(args); break;
                case "source":
                case ".": CmdSource(args); break;
                case "clear": Console.Clear(); break;
                case "exit":
                case "logout":
                    _running = false;
                    break;
                default:
                    if (!_scriptRunner.TryRunCommand(cmd, args, _currentUser, _cwd))
                    {
                        if (!TryRunShellScript(cmd, args))
                            Console.WriteLine($"nsh: {cmd}: command not found");
                    }
                    break;
            }
        }
        finally
        {
            if (capture != null)
            {
                Console.SetOut(originalOut);
                string output = capture.ToString();
                string fullPath = VirtualFileSystem.ResolvePath(_cwd, redirectFile!);

                // Permission check for redirection target
                bool allowed = true;
                if (_fs.IsFile(fullPath))
                {
                    var node = _fs.GetNode(fullPath);
                    if (node != null && !node.CanWrite(_currentUser.Uid, _currentUser.Gid))
                    {
                        Console.WriteLine($"nsh: {redirectFile}: Permission denied");
                        allowed = false;
                    }
                }
                else
                {
                    string parent = VirtualFileSystem.GetParent(fullPath);
                    var parentNode = _fs.GetNode(parent);
                    if (parentNode != null && !parentNode.CanWrite(_currentUser.Uid, _currentUser.Gid))
                    {
                        Console.WriteLine($"nsh: {redirectFile}: Permission denied");
                        allowed = false;
                    }
                }

                if (allowed)
                {
                    byte[] existing = [];
                    if (appendMode && _fs.IsFile(fullPath))
                        existing = _fs.ReadFile(fullPath);

                    byte[] newData = Encoding.UTF8.GetBytes(output);
                    byte[] combined = [.. existing, .. newData];

                    if (_fs.IsFile(fullPath))
                        _fs.WriteFile(fullPath, combined);
                    else
                        _fs.CreateFile(fullPath, _currentUser.Uid, _currentUser.Gid, combined);

                    _fs.Save();
                }
            }
        }
    }

    // ?? Builtin commands ???????????????????????????????????????????

    private void CmdHelp()
    {
        Console.WriteLine("""
        NetNIX Shell (nsh) — Available commands:

          Use 'man <command>' for detailed help on any command.
          Use 'man --list' to see all manual pages.

          Shell builtins:
            help  man  cd  edit  write  chmod  chown  stat  tree
            adduser  deluser  passwd  su  sudo  users  groups
            run  source  clear  exit/logout

          Script commands (/bin/*.cs):
            ls  cat  cp  mv  rm  mkdir  rmdir  touch
            head  tail  wc  grep  find  tee  echo  pwd
            whoami  id  uname  hostname  date  env
            basename  dirname  du  df  yes  true  false
            curl  wget  fetch  cbpaste  cbcopy  zip  unzip
            mount  umount  export
            useradd  userdel  usermod
            groupadd  groupdel  groupmod

          Help topics:
            man api             NixApi scripting reference
            man scripting       How to write .cs scripts
            man include         Library #include system
            man editor          Text editor guide
            man filesystem      Filesystem hierarchy
            man permissions     File permission system
            man sandbox         Script sandbox security (/etc/sandbox.conf)
            man nshrc           Shell startup scripts

          Shell scripts:
            source <file>       Execute a shell script (one command per line)
            . <file>            Alias for source
            ~/.nshrc            Runs automatically on login

          Any unknown command is looked up as a .cs script
          in /bin, /usr/bin, /usr/local/bin, and the cwd.
        """);
    }

    private void CmdRun(List<string> args)
    {
        if (args.Count == 0) { Console.WriteLine("run: usage: run <file.cs> [args...]"); return; }

        string path = VirtualFileSystem.ResolvePath(_cwd, args[0]);
        var scriptArgs = args.Skip(1).ToArray();
        _scriptRunner.RunFile(path, scriptArgs, _currentUser, _cwd);
    }

    private void CmdEdit(List<string> args)
    {
        if (args.Count == 0) { Console.WriteLine("edit: usage: edit <file>"); return; }

        string path = VirtualFileSystem.ResolvePath(_cwd, args[0]);

        if (_fs.IsDirectory(path))
        {
            Console.WriteLine($"edit: {args[0]}: Is a directory");
            return;
        }

        if (_fs.IsFile(path))
        {
            var node = _fs.GetNode(path);
            if (node != null && !node.CanRead(_currentUser.Uid, _currentUser.Gid))
            {
                Console.WriteLine($"edit: {args[0]}: Permission denied");
                return;
            }
            if (node != null && !node.CanWrite(_currentUser.Uid, _currentUser.Gid))
            {
                Console.WriteLine($"edit: {args[0]}: Permission denied (read-only)");
                return;
            }
        }
        else
        {
            // Creating a new file: check write permission on the parent directory
            string parent = VirtualFileSystem.GetParent(path);
            var parentNode = _fs.GetNode(parent);
            if (parentNode != null && !parentNode.CanWrite(_currentUser.Uid, _currentUser.Gid))
            {
                Console.WriteLine($"edit: {args[0]}: Permission denied");
                return;
            }
        }

        var editor = new TextEditor(_fs, path, _currentUser.Uid, _currentUser.Gid);
        editor.Run();
    }

    private void CmdMan(List<string> args)
    {
        const string manDir = "/usr/share/man";

        if (args.Count == 0)
        {
            Console.WriteLine("Usage: man <topic>");
            Console.WriteLine("       man -k <keyword>   Search pages");
            Console.WriteLine("       man --list          List all pages");
            return;
        }

        // man --list: show all available pages
        if (args[0] == "--list")
        {
            if (!_fs.IsDirectory(manDir))
            {
                Console.WriteLine("man: no manual pages installed");
                return;
            }
            var pages = _fs.ListDirectory(manDir).OrderBy(n => n.Name).ToList();
            if (pages.Count == 0)
            {
                Console.WriteLine("man: no manual pages installed");
                return;
            }
            Console.WriteLine("Available manual pages:\n");
            int col = 0;
            foreach (var page in pages)
            {
                string name = page.Name.EndsWith(".txt") ? page.Name[..^4] : page.Name;
                Console.Write($"  {name,-18}");
                col++;
                if (col % 4 == 0) Console.WriteLine();
            }
            if (col % 4 != 0) Console.WriteLine();
            Console.WriteLine($"\n{pages.Count} pages available. Use 'man <topic>' to read.");
            return;
        }

        // man -k <keyword>: search pages
        if (args[0] == "-k" && args.Count > 1)
        {
            string keyword = args[1];
            if (!_fs.IsDirectory(manDir)) { Console.WriteLine("man: no manual pages installed"); return; }

            var pages = _fs.ListDirectory(manDir).OrderBy(n => n.Name).ToList();
            bool found = false;
            foreach (var page in pages)
            {
                string content = Encoding.UTF8.GetString(_fs.ReadFile(page.Path));
                if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    string name = page.Name.EndsWith(".txt") ? page.Name[..^4] : page.Name;
                    // Extract the NAME line for a brief description
                    string desc = ExtractManDescription(content);
                    Console.WriteLine($"  {name,-18} {desc}");
                    found = true;
                }
            }
            if (!found)
                Console.WriteLine($"man: nothing found for '{keyword}'");
            return;
        }

        // man <topic>: display a page
        string topic = args[0].ToLowerInvariant();
        string filePath = $"{manDir}/{topic}.txt";

        if (!_fs.IsFile(filePath))
        {
            Console.WriteLine($"man: no manual entry for '{topic}'");
            Console.WriteLine($"     Use 'man --list' to see available pages.");
            Console.WriteLine($"     Create one with: edit {filePath}");
            return;
        }

        string text = Encoding.UTF8.GetString(_fs.ReadFile(filePath));
        PageOutput(text);
    }

    private static string ExtractManDescription(string content)
    {
        var lines = content.Split('\n');
        bool foundName = false;
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed == "NAME")
            {
                foundName = true;
                continue;
            }
            if (foundName && trimmed.Length > 0)
                return trimmed;
        }
        return "";
    }

    private static void PageOutput(string text)
    {
        var lines = text.Split('\n');
        int pageSize = Console.WindowHeight - 2;
        if (pageSize < 5) pageSize = 20;

        for (int i = 0; i < lines.Length; i++)
        {
            Console.WriteLine(lines[i]);

            if ((i + 1) % pageSize == 0 && i + 1 < lines.Length)
            {
                Console.Write("\x1b[7m -- Press any key for more, q to quit -- \x1b[0m");
                var key = Console.ReadKey(intercept: true);
                Console.Write("\r\x1b[K"); // clear the prompt line
                if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    return;
            }
        }
    }

    private void CmdCd(List<string> args)
    {
        string target;
        if (args.Count == 0 || args[0] == "~")
            target = _currentUser.HomeDirectory;
        else if (args[0] == "-")
            target = _cwd;
        else
            target = VirtualFileSystem.ResolvePath(_cwd, args[0]);

        if (!_fs.IsDirectory(target))
        {
            Console.WriteLine($"cd: {args.FirstOrDefault() ?? "~"}: No such directory");
            return;
        }

        var node = _fs.GetNode(target);
        if (node != null && !node.CanExecute(_currentUser.Uid, _currentUser.Gid))
        {
            Console.WriteLine($"cd: {args.FirstOrDefault() ?? "~"}: Permission denied");
            return;
        }

        _cwd = target;
    }

    private void CmdWrite(List<string> args)
    {
        if (args.Count == 0) { Console.WriteLine("write: usage: write <file>"); return; }

        string path = VirtualFileSystem.ResolvePath(_cwd, args[0]);

        // Permission check: need write on existing file, or write on parent to create
        if (_fs.IsFile(path))
        {
            var node = _fs.GetNode(path);
            if (node != null && !node.CanWrite(_currentUser.Uid, _currentUser.Gid))
            {
                Console.WriteLine($"write: {args[0]}: Permission denied");
                return;
            }
        }
        else
        {
            string parent = VirtualFileSystem.GetParent(path);
            var parentNode = _fs.GetNode(parent);
            if (parentNode != null && !parentNode.CanWrite(_currentUser.Uid, _currentUser.Gid))
            {
                Console.WriteLine($"write: {args[0]}: Permission denied");
                return;
            }
        }

        Console.WriteLine("Enter text (type a single '.' on a line to finish):");

        var sb = new StringBuilder();
        while (true)
        {
            string? line = Console.ReadLine();
            if (line == null || line == ".") break;
            sb.AppendLine(line);
        }

        var data = Encoding.UTF8.GetBytes(sb.ToString());
        if (_fs.IsFile(path))
            _fs.WriteFile(path, data);
        else
            _fs.CreateFile(path, _currentUser.Uid, _currentUser.Gid, data);

        _fs.Save();
    }

    private void CmdChmod(List<string> args)
    {
        if (args.Count < 2) { Console.WriteLine("chmod: usage: chmod <perms> <path>"); return; }

        string perms = args[0];
        string path = VirtualFileSystem.ResolvePath(_cwd, args[1]);
        var node = _fs.GetNode(path);
        if (node == null) { Console.WriteLine($"chmod: {args[1]}: No such file or directory"); return; }

        if (_currentUser.Uid != 0 && node.OwnerId != _currentUser.Uid)
        {
            Console.WriteLine("chmod: Permission denied");
            return;
        }

        if (perms.Length == 9 && perms.All(c => "rwx-".Contains(c)))
        {
            node.Permissions = perms;
        }
        else if (perms.Length == 3 && perms.All(char.IsDigit))
        {
            node.Permissions = OctalToPermString(perms);
        }
        else
        {
            Console.WriteLine("chmod: invalid mode — use rwxr-xr-x or 755 format");
            return;
        }

        _fs.Save();
    }

    private void CmdChown(List<string> args)
    {
        if (args.Count < 2) { Console.WriteLine("chown: usage: chown <user> <path>"); return; }
        if (_currentUser.Uid != 0) { Console.WriteLine("chown: Permission denied (must be root)"); return; }

        string path = VirtualFileSystem.ResolvePath(_cwd, args[1]);
        var node = _fs.GetNode(path);
        if (node == null) { Console.WriteLine($"chown: {args[1]}: No such file or directory"); return; }

        var user = _userMgr.GetUser(args[0]);
        if (user == null) { Console.WriteLine($"chown: unknown user '{args[0]}'"); return; }

        node.OwnerId = user.Uid;
        node.GroupId = user.Gid;
        _fs.Save();
    }

    private void CmdAddUser(List<string> args)
    {
        if (_currentUser.Uid != 0) { Console.WriteLine("adduser: Permission denied (must be root)"); return; }
        if (args.Count == 0) { Console.WriteLine("adduser: usage: adduser <username>"); return; }

        string username = args[0];
        Console.Write($"New password for {username}: ");
        string? pass = ReadPassword();
        if (string.IsNullOrEmpty(pass)) { Console.WriteLine("adduser: aborted"); return; }

        _userMgr.CreateUser(username, pass);
        _fs.Save();
        Console.WriteLine($"User '{username}' created.");
    }

    private void CmdDelUser(List<string> args)
    {
        if (_currentUser.Uid != 0) { Console.WriteLine("deluser: Permission denied (must be root)"); return; }
        if (args.Count == 0) { Console.WriteLine("deluser: usage: deluser <username>"); return; }

        _userMgr.DeleteUser(args[0]);
        _fs.Save();
        Console.WriteLine($"User '{args[0]}' deleted.");
    }

    private void CmdPasswd(List<string> args)
    {
        string target = args.Count > 0 ? args[0] : _currentUser.Username;

        if (target != _currentUser.Username && _currentUser.Uid != 0)
        {
            Console.WriteLine("passwd: Permission denied");
            return;
        }

        Console.Write($"New password for {target}: ");
        string? pass = ReadPassword();
        if (string.IsNullOrEmpty(pass)) { Console.WriteLine("passwd: aborted"); return; }

        _userMgr.ChangePassword(target, pass);
        _fs.Save();
        Console.WriteLine("Password updated.");
    }

    private void CmdSu(List<string> args)
    {
        if (args.Count == 0) { Console.WriteLine("su: usage: su <username>"); return; }

        var target = _userMgr.GetUser(args[0]);
        if (target == null) { Console.WriteLine($"su: user '{args[0]}' does not exist"); return; }

        if (_currentUser.Uid != 0)
        {
            Console.Write($"Password for {target.Username}: ");
            string? pass = ReadPassword();
            if (pass == null || !target.VerifyPassword(pass))
            {
                Console.WriteLine("su: Authentication failure");
                return;
            }
        }

        _currentUser = target;
        _cwd = target.HomeDirectory;
        if (!_fs.Exists(_cwd)) _cwd = "/";
        Console.WriteLine($"Switched to {target.Username}");
    }

    private void CmdSudo(List<string> args)
    {
        if (args.Count == 0)
        {
            Console.WriteLine("sudo: usage: sudo <command> [args...]");
            return;
        }

        // Root doesn't need sudo
        if (_currentUser.Uid == 0)
        {
            Execute(string.Join(' ', args));
            return;
        }

        // Check that the current user is in the 'sudo' group
        var sudoGroup = _userMgr.GetGroup("sudo");
        if (sudoGroup == null || !sudoGroup.Members.Contains(_currentUser.Username))
        {
            Console.WriteLine($"sudo: {_currentUser.Username} is not in the sudo group");
            return;
        }

        // Prompt for the user's own password
        Console.Write($"[sudo] password for {_currentUser.Username}: ");
        string? pass = ReadPassword();
        if (pass == null || !_currentUser.VerifyPassword(pass))
        {
            Console.WriteLine("sudo: authentication failure");
            return;
        }

        // Temporarily elevate to root
        var rootUser = _userMgr.GetUser(0);
        if (rootUser == null)
        {
            Console.WriteLine("sudo: root account not found");
            return;
        }

        var previousUser = _currentUser;
        var previousCwd = _cwd;
        _currentUser = rootUser;

        try
        {
            Execute(string.Join(' ', args));
        }
        finally
        {
            _currentUser = previousUser;
            _cwd = previousCwd;
        }
    }

    private void CmdUsers()
    {
        foreach (var u in _userMgr.Users)
        {
            var grp = _userMgr.GetGroup(u.Gid);
            Console.WriteLine($"  {u.Username,-16} uid={u.Uid}  gid={u.Gid}({grp?.Name ?? "?"})  home={u.HomeDirectory}");
        }
    }

    private void CmdGroups()
    {
        foreach (var g in _userMgr.Groups)
            Console.WriteLine($"  {g.Name,-16} gid={g.Gid}  members={string.Join(',', g.Members)}");
    }

    private void CmdStat(List<string> args)
    {
        if (args.Count == 0) { Console.WriteLine("stat: missing operand"); return; }

        string path = VirtualFileSystem.ResolvePath(_cwd, args[0]);
        var node = _fs.GetNode(path);
        if (node == null) { Console.WriteLine($"stat: {args[0]}: No such file or directory"); return; }

        var owner = _userMgr.GetUser(node.OwnerId);
        var group = _userMgr.GetGroup(node.GroupId);

        Console.WriteLine($"  File: {node.Path}");
        Console.WriteLine($"  Type: {(node.IsDirectory ? "directory" : "regular file")}");
        Console.WriteLine($"  Size: {(node.IsDirectory ? "-" : (node.Data?.Length ?? 0).ToString())} bytes");
        Console.WriteLine($"  Mode: {node.PermissionString()}");
        Console.WriteLine($"  Owner: {owner?.Username ?? node.OwnerId.ToString()} (uid={node.OwnerId})");
        Console.WriteLine($"  Group: {group?.Name ?? node.GroupId.ToString()} (gid={node.GroupId})");
    }

    private void CmdTree(List<string> args)
    {
        string root = args.Count > 0 ? VirtualFileSystem.ResolvePath(_cwd, args[0]) : _cwd;
        if (!_fs.IsDirectory(root))
        {
            Console.WriteLine($"tree: {args.FirstOrDefault() ?? "."}: Not a directory");
            return;
        }

        Console.WriteLine(root == "/" ? "/" : VirtualFileSystem.GetName(root));
        PrintTree(root, "");
    }

    private void PrintTree(string dir, string indent)
    {
        var children = _fs.ListDirectory(dir).OrderBy(n => n.Name).ToList();
        for (int i = 0; i < children.Count; i++)
        {
            bool last = i == children.Count - 1;
            string connector = last ? "??? " : "??? ";
            string childIndent = last ? "    " : "?   ";

            var child = children[i];
            if (child.IsDirectory)
            {
                Console.WriteLine($"{indent}{connector}\u001b[34m{child.Name}/\u001b[0m");
                PrintTree(child.Path, indent + childIndent);
            }
            else
            {
                Console.WriteLine($"{indent}{connector}{child.Name}");
            }
        }
    }

    // ?? Utility ????????????????????????????????????????????????????

    private static string OctalToPermString(string octal)
    {
        static string Digit(char c)
        {
            int v = c - '0';
            return $"{((v & 4) != 0 ? 'r' : '-')}{((v & 2) != 0 ? 'w' : '-')}{((v & 1) != 0 ? 'x' : '-')}";
        }

        return Digit(octal[0]) + Digit(octal[1]) + Digit(octal[2]);
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;
        char quoteChar = '"';

        foreach (char c in input)
        {
            if (inQuote)
            {
                if (c == quoteChar)
                    inQuote = false;
                else
                    sb.Append(c);
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (c == ' ')
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
            tokens.Add(sb.ToString());

        return tokens;
    }

    private static string? ReadPassword()
    {
        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else
            {
                sb.Append(key.KeyChar);
                Console.Write('*');
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    // ?? Shell scripts ?????????????????????????????????????????????????

    private static readonly string[] ShellSearchDirs = ["/bin", "/usr/bin", "/usr/local/bin"];

    /// <summary>
    /// Attempts to find and run a shell script (.sh file or file with #!/bin/nsh shebang).
    /// Returns true if a script was found, false otherwise.
    /// </summary>
    private bool TryRunShellScript(string command, List<string> args)
    {
        string? scriptPath = ResolveShellScript(command);
        if (scriptPath == null)
            return false;

        var node = _fs.GetNode(scriptPath);
        if (node == null || node.IsDirectory)
            return false;

        if (!node.CanRead(_currentUser.Uid, _currentUser.Gid))
        {
            Console.WriteLine($"nsh: {command}: Permission denied");
            return true;
        }

        // Verify it looks like a shell script (not a .cs file)
        string content = Encoding.UTF8.GetString(_fs.ReadFile(scriptPath));
        if (scriptPath.EndsWith(".cs") && !content.TrimStart().StartsWith('#'))
            return false; // Let the "command not found" message show

        SourceFile(scriptPath);
        return true;
    }

    private string? ResolveShellScript(string command)
    {
        // Explicit path (contains /)
        if (command.Contains('/'))
        {
            string resolved = VirtualFileSystem.ResolvePath(_cwd, command);
            if (_fs.IsFile(resolved) && IsShellScript(resolved)) return resolved;
            if (_fs.IsFile(resolved + ".sh")) return resolved + ".sh";
            return null;
        }

        // Check current working directory
        string cwdSh = _cwd.TrimEnd('/') + "/" + command + ".sh";
        if (_fs.IsFile(cwdSh)) return cwdSh;

        string cwdBare = _cwd.TrimEnd('/') + "/" + command;
        if (_fs.IsFile(cwdBare) && IsShellScript(cwdBare)) return cwdBare;

        // Search PATH directories
        foreach (var dir in ShellSearchDirs)
        {
            string shPath = dir + "/" + command + ".sh";
            if (_fs.IsFile(shPath)) return shPath;

            string barePath = dir + "/" + command;
            if (_fs.IsFile(barePath) && IsShellScript(barePath)) return barePath;
        }

        return null;
    }

    /// <summary>
    /// Returns true if the file at the given VFS path appears to be a shell script
    /// (has .sh extension, or starts with a #!shebang / # comment).
    /// </summary>
    private bool IsShellScript(string vfsPath)
    {
        if (vfsPath.EndsWith(".sh")) return true;

        // Peek at the content to check for shebang or comment header
        var node = _fs.GetNode(vfsPath);
        if (node?.Data == null || node.Data.Length == 0) return false;

        string firstLine = Encoding.UTF8.GetString(node.Data).TrimStart();
        return firstLine.StartsWith('#');
    }

    private void RunStartupScript()
    {
        string rcPath = _currentUser.HomeDirectory.TrimEnd('/') + "/.nshrc";
        if (_fs.IsFile(rcPath))
        {
            var node = _fs.GetNode(rcPath);
            if (node != null && node.CanRead(_currentUser.Uid, _currentUser.Gid))
                SourceFile(rcPath);
        }
    }

    private void CmdSource(List<string> args)
    {
        if (args.Count == 0)
        {
            Console.WriteLine("source: usage: source <file>");
            return;
        }

        string path = VirtualFileSystem.ResolvePath(_cwd, args[0]);

        if (!_fs.IsFile(path))
        {
            Console.WriteLine($"source: {args[0]}: No such file");
            return;
        }

        var node = _fs.GetNode(path);
        if (node != null && !node.CanRead(_currentUser.Uid, _currentUser.Gid))
        {
            Console.WriteLine($"source: {args[0]}: Permission denied");
            return;
        }

        SourceFile(path);
    }

    private void SourceFile(string vfsPath)
    {
        string content = Encoding.UTF8.GetString(_fs.ReadFile(vfsPath));
        var lines = content.Replace("\r\n", "\n").Split('\n');

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();

            // Skip blank lines and comments
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            // Expand shell variables
            line = ExpandVariables(line);

            try
            {
                Execute(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"nsh: {vfsPath}: {ex.Message}");
            }

            // Stop if the script caused an exit
            if (!_running) break;
        }
    }

    private string ExpandVariables(string line)
    {
        var sb = new StringBuilder(line);
        sb.Replace("$USER", _currentUser.Username);
        sb.Replace("$UID", _currentUser.Uid.ToString());
        sb.Replace("$GID", _currentUser.Gid.ToString());
        sb.Replace("$HOME", _currentUser.HomeDirectory);
        sb.Replace("$CWD", _cwd);
        sb.Replace("$PWD", _cwd);
        sb.Replace("$SHELL", "/bin/nsh");
        sb.Replace("$HOSTNAME", "netnix");
        sb.Replace("~", _currentUser.HomeDirectory);
        return sb.ToString();
    }
}
