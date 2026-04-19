/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
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
    private readonly DaemonManager _daemonMgr;
    private UserRecord _currentUser;
    private string _cwd;
    private bool _running = true;

    /// <summary>
    /// True when the current command is receiving piped input.
    /// Scripts can check this to determine if stdin has data.
    /// </summary>
    public static bool IsPiped { get; set; }

    public NixShell(VirtualFileSystem fs, UserManager userMgr, ScriptRunner scriptRunner, DaemonManager daemonMgr, UserRecord user)
    {
        _fs = fs;
        _userMgr = userMgr;
        _scriptRunner = scriptRunner;
        _daemonMgr = daemonMgr;
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
                ExecuteLine(input);
            }
            catch (Exception ex)
            {
                Console.ResetColor();
                Console.WriteLine($"nsh: unhandled exception: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // —— Command dispatcher ———————————————————————————————————————————

    /// <summary>
    /// Top-level input handler. Splits on unquoted pipe characters and
    /// chains commands so that each command's stdout becomes the next
    /// command's stdin — just like UNIX pipes.
    /// </summary>
    private void ExecuteLine(string input)
    {
        // Split input on unquoted '|'
        var segments = SplitPipes(input);

        if (segments.Count == 1)
        {
            // No pipe — run directly (fast path, no extra StringWriter)
            Execute(segments[0].Trim());
            return;
        }

        // Pipeline: capture stdout of each stage, feed as stdin to the next
        TextWriter originalOut = Console.Out;
        TextReader originalIn = Console.In;
        string previousOutput = null;

        try
        {
            for (int i = 0; i < segments.Count; i++)
            {
                string segment = segments[i].Trim();
                if (segment.Length == 0) continue;

                bool isLast = (i == segments.Count - 1);

                // Feed previous command's output as this command's stdin
                if (previousOutput != null)
                {
                    Console.SetIn(new StringReader(previousOutput));
                    IsPiped = true;
                }
                else
                {
                    IsPiped = false;
                }

                // Capture this command's output (unless it's the last stage)
                StringWriter? stageCapture = null;
                if (!isLast)
                {
                    stageCapture = new StringWriter();
                    Console.SetOut(stageCapture);
                }
                else
                {
                    // Last stage writes to the real console (or redirect handles it)
                    Console.SetOut(originalOut);
                }

                try
                {
                    Execute(segment);
                }
                catch (Exception ex)
                {
                    Console.SetOut(originalOut);
                    Console.SetIn(originalIn);
                    Console.ResetColor();
                    Console.Error.WriteLine($"nsh: pipe stage {i + 1}: {ex.GetType().Name}: {ex.Message}");
                    return;
                }

                if (stageCapture != null)
                {
                    Console.SetOut(originalOut);
                    previousOutput = stageCapture.ToString();
                }
                else
                {
                    previousOutput = null;
                }
            }
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetIn(originalIn);
            IsPiped = false;
        }
    }

    /// <summary>
    /// Split an input line on unquoted '|' characters.
    /// Respects single and double quotes.
    /// </summary>
    private static List<string> SplitPipes(string input)
    {
        var segments = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;
        char quoteChar = '"';

        foreach (char c in input)
        {
            if (inQuote)
            {
                if (c == quoteChar)
                    inQuote = false;
                sb.Append(c);
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = true;
                quoteChar = c;
                sb.Append(c);
            }
            else if (c == '|')
            {
                segments.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
            segments.Add(sb.ToString());

        return segments;
    }

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
                case "daemon": CmdDaemon(args); break;
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
        catch (Exception ex)
        {
            // Restore console state before printing
            if (capture != null)
            {
                Console.SetOut(originalOut);
                capture = null;
            }
            Console.ResetColor();
            Console.Error.WriteLine($"nsh: {cmd}: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            try
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
            catch (Exception ex)
            {
                Console.SetOut(originalOut);
                Console.ResetColor();
                Console.Error.WriteLine($"nsh: redirect error: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // —— Builtin commands ———————————————————————————————————————————

    private void CmdHelp()
    {
        Console.WriteLine("""
        NetNIX Shell (nsh) — Available commands:

          Use 'man <command>' for detailed help on any command.
          Use 'man --list' to see all manual pages.

          Shell builtins:
            help  man  cd  write  chmod  chown  stat  tree
            adduser  deluser  passwd  su  sudo  users  groups
            run  source  daemon  clear  exit/logout

          Script commands (/bin/*.cs):
            ls  cat  cp  mv  rm  mkdir  rmdir  touch
            head  tail  wc  grep  find  tee  echo  pwd
            whoami  id  uname  hostname  date  env
            basename  dirname  du  df  yes  true  false
            curl  wget  fetch  cbpaste  cbcopy  zip  unzip
            edit  nxconfig  settings-demo

          System admin commands (/sbin/*.cs — root/sudo):
            useradd  userdel  usermod
            groupadd  groupdel  groupmod
            mount  umount  export  importfile  reinstall
            npak  npak-demo  httpd

          Help topics:
            man api                  NixApi scripting reference
            man scripting            How to write .cs scripts
            man include              Library #include system
            man editor               Text editor guide
            man filesystem           Filesystem hierarchy
            man permissions          File permission system
            man sandbox              Script sandbox security
            man sandbox.exceptions   Per-script sandbox overrides
            man settingslib          Application settings library
            man dotnet_lib_emu       .NET System.IO emulation library
            man nshrc                Shell startup scripts
            man daemon               Daemon management commands
            man daemon-writing       How to write daemon scripts
            man httpd                Built-in HTTP server daemon

          Additional packages (install via npak):
            npak get install kobold      AI chat client for KoboldCpp
            npak get install nxai        Unified AI chat interface
            npak get install koder       AI command generator

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
            ExecuteLine(string.Join(' ', args));
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
            ExecuteLine(string.Join(' ', args));
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
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

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

        SourceFile(scriptPath, args.ToArray());
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

    private void SourceFile(string vfsPath, string[]? scriptArgs = null)
    {
        string content = Encoding.UTF8.GetString(_fs.ReadFile(vfsPath));
        var lines = content.Replace("\r\n", "\n").Split('\n');

        // Script-local variables (includes positional params $1-$9)
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        vars["0"] = vfsPath;
        if (scriptArgs != null)
        {
            for (int i = 0; i < scriptArgs.Length && i < 9; i++)
                vars[(i + 1).ToString()] = scriptArgs[i];
            vars["#"] = scriptArgs.Length.ToString();
            vars["@"] = string.Join(" ", scriptArgs);
        }
        else
        {
            vars["#"] = "0";
            vars["@"] = "";
        }
        vars["?"] = "0"; // last exit code

        InterpretLines(lines, 0, lines.Length, vars, vfsPath);
    }

    /// <summary>
    /// Interpret shell lines from startIdx (inclusive) to endIdx (exclusive).
    /// Supports: if/elif/else/fi, for/do/done, while/do/done, case/esac,
    /// variable assignment (VAR=value), command substitution $(cmd),
    /// and all standard variable expansions.
    /// Returns the index of the line after the last consumed line.
    /// </summary>
    private int InterpretLines(string[] lines, int startIdx, int endIdx,
        Dictionary<string, string> vars, string label)
    {
        int i = startIdx;
        while (i < endIdx && _running)
        {
            string raw = lines[i].Trim();

            // Skip blank lines, comments, shebang
            if (raw.Length == 0 || raw.StartsWith('#'))
            {
                i++;
                continue;
            }

            // Expand variables
            string line = ExpandScriptVars(raw, vars);

            // ?? Variable assignment: VAR=value ?????????????????????
            if (IsAssignment(line, out string aKey, out string aVal))
            {
                vars[aKey] = ExpandCommandSubstitutions(aVal, vars, label);
                i++;
                continue;
            }

            // ?? if / elif / else / fi ??????????????????????????????
            if (line.StartsWith("if "))
            {
                i = HandleIf(lines, i, endIdx, vars, label);
                continue;
            }

            // ?? for VAR in ... ; do ... done ???????????????????????
            if (line.StartsWith("for "))
            {
                i = HandleFor(lines, i, endIdx, vars, label);
                continue;
            }

            // ?? while ... ; do ... done ????????????????????????????
            if (line.StartsWith("while "))
            {
                i = HandleWhile(lines, i, endIdx, vars, label);
                continue;
            }

            // ?? case VAR in ... esac ???????????????????????????????
            if (line.StartsWith("case "))
            {
                i = HandleCase(lines, i, endIdx, vars, label);
                continue;
            }

            // ?? break / continue (handled by loop callers) ?????????
            if (line == "break" || line == "continue")
            {
                // These are caught by loop handlers via _shellBreak/_shellContinue
                if (line == "break") _shellBreak = true;
                else _shellContinue = true;
                i++;
                return i;
            }

            // ?? Regular command ?????????????????????????????????????
            line = ExpandCommandSubstitutions(line, vars, label);

            try
            {
                ExecuteLine(line);
                vars["?"] = "0";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"nsh: {label}: {ex.Message}");
                vars["?"] = "1";
            }

            if (!_running) break;
            i++;
        }
        return i;
    }

    // Flags for break/continue in loops
    private bool _shellBreak;
    private bool _shellContinue;

    // ?? if/elif/else/fi ????????????????????????????????????????????

    private int HandleIf(string[] lines, int startIdx, int endIdx,
        Dictionary<string, string> vars, string label)
    {
        // Collect the if block structure
        // if CONDITION; then     (or "if CONDITION" + next line "then")
        // ... body ...
        // elif CONDITION; then
        // ... body ...
        // else
        // ... body ...
        // fi

        int i = startIdx;
        bool anyBranchTaken = false;

        // Parse "if CONDITION" or "if CONDITION; then"
        string condition = ExtractCondition(lines[i].Trim(), "if ");
        bool hasThen = condition.EndsWith("; then") || condition.EndsWith(";then");
        if (hasThen)
            condition = condition.Substring(0, condition.LastIndexOf(';')).Trim();
        i++;

        if (!hasThen)
        {
            // Next line should be "then"
            while (i < endIdx && lines[i].Trim().Length == 0) i++;
            if (i < endIdx && lines[i].Trim() == "then") i++;
        }

        // Collect body until elif/else/fi
        int bodyStart = i;
        int bodyEnd = FindBlockEnd(lines, i, endIdx, new[] { "elif", "else", "fi" });

        if (!anyBranchTaken && EvaluateCondition(ExpandScriptVars(condition, vars), vars, label))
        {
            InterpretLines(lines, bodyStart, bodyEnd, vars, label);
            anyBranchTaken = true;
        }
        i = bodyEnd;

        // Handle elif / else chains
        while (i < endIdx)
        {
            string cur = lines[i].Trim();
            string expanded = ExpandScriptVars(cur, vars);

            if (expanded == "fi")
            {
                i++;
                break;
            }

            if (expanded.StartsWith("elif "))
            {
                condition = ExtractCondition(expanded, "elif ");
                hasThen = condition.EndsWith("; then") || condition.EndsWith(";then");
                if (hasThen)
                    condition = condition.Substring(0, condition.LastIndexOf(';')).Trim();
                i++;
                if (!hasThen)
                {
                    while (i < endIdx && lines[i].Trim().Length == 0) i++;
                    if (i < endIdx && lines[i].Trim() == "then") i++;
                }

                bodyStart = i;
                bodyEnd = FindBlockEnd(lines, i, endIdx, new[] { "elif", "else", "fi" });

                if (!anyBranchTaken && EvaluateCondition(condition, vars, label))
                {
                    InterpretLines(lines, bodyStart, bodyEnd, vars, label);
                    anyBranchTaken = true;
                }
                i = bodyEnd;
                continue;
            }

            if (expanded == "else")
            {
                i++;
                bodyStart = i;
                bodyEnd = FindBlockEnd(lines, i, endIdx, new[] { "fi" });

                if (!anyBranchTaken)
                {
                    InterpretLines(lines, bodyStart, bodyEnd, vars, label);
                    anyBranchTaken = true;
                }
                i = bodyEnd;
                continue;
            }

            // Unexpected token, skip
            i++;
        }

        return i;
    }

    // ?? for VAR in LIST; do ... done ???????????????????????????????

    private int HandleFor(string[] lines, int startIdx, int endIdx,
        Dictionary<string, string> vars, string label)
    {
        // for VAR in item1 item2 item3; do
        // or:
        // for VAR in item1 item2 item3
        // do
        string header = ExpandScriptVars(lines[startIdx].Trim(), vars);
        header = ExpandCommandSubstitutions(header, vars, label);

        // Parse: "for VAR in ..."
        string rest = header.Substring(4).Trim(); // after "for "
        int inIdx = rest.IndexOf(" in ");
        if (inIdx < 0) return startIdx + 1; // malformed

        string varName = rest.Substring(0, inIdx).Trim();
        string listPart = rest.Substring(inIdx + 4).Trim();

        bool hasDo = listPart.EndsWith("; do") || listPart.EndsWith(";do");
        if (hasDo)
            listPart = listPart.Substring(0, listPart.LastIndexOf(';')).Trim();

        int i = startIdx + 1;
        if (!hasDo)
        {
            while (i < endIdx && lines[i].Trim().Length == 0) i++;
            if (i < endIdx && lines[i].Trim() == "do") i++;
        }

        // Find matching done
        int bodyStart = i;
        int bodyEnd = FindNestedBlockEnd(lines, i, endIdx, "do", "done");

        // Parse list items (space-separated, supports glob-like *)
        var items = ParseForList(listPart, vars, label);

        foreach (var item in items)
        {
            vars[varName] = item;
            _shellBreak = false;
            _shellContinue = false;
            InterpretLines(lines, bodyStart, bodyEnd, vars, label);
            if (_shellBreak) { _shellBreak = false; break; }
            if (_shellContinue) { _shellContinue = false; continue; }
            if (!_running) break;
        }

        // Skip past "done"
        return bodyEnd < endIdx ? bodyEnd + 1 : bodyEnd;
    }

    // ?? while CONDITION; do ... done ???????????????????????????????

    private int HandleWhile(string[] lines, int startIdx, int endIdx,
        Dictionary<string, string> vars, string label)
    {
        string header = lines[startIdx].Trim();
        string condition = ExtractCondition(header, "while ");
        bool hasDo = condition.EndsWith("; do") || condition.EndsWith(";do");
        if (hasDo)
            condition = condition.Substring(0, condition.LastIndexOf(';')).Trim();

        int i = startIdx + 1;
        if (!hasDo)
        {
            while (i < endIdx && lines[i].Trim().Length == 0) i++;
            if (i < endIdx && lines[i].Trim() == "do") i++;
        }

        int bodyStart = i;
        int bodyEnd = FindNestedBlockEnd(lines, i, endIdx, "do", "done");

        int maxIter = 10000; // safety limit
        int iter = 0;
        while (iter++ < maxIter && _running)
        {
            string expandedCond = ExpandScriptVars(condition, vars);
            if (!EvaluateCondition(expandedCond, vars, label))
                break;

            _shellBreak = false;
            _shellContinue = false;
            InterpretLines(lines, bodyStart, bodyEnd, vars, label);
            if (_shellBreak) { _shellBreak = false; break; }
            if (_shellContinue) { _shellContinue = false; continue; }
        }

        return bodyEnd < endIdx ? bodyEnd + 1 : bodyEnd;
    }

    // ?? case VAR in ... esac ???????????????????????????????????????

    private int HandleCase(string[] lines, int startIdx, int endIdx,
        Dictionary<string, string> vars, string label)
    {
        // case WORD in
        //   pattern1) commands ;;
        //   pattern2) commands ;;
        //   *) commands ;;
        // esac

        string header = ExpandScriptVars(lines[startIdx].Trim(), vars);
        // "case WORD in"
        string rest = header.Substring(5).Trim(); // after "case "
        if (rest.EndsWith(" in"))
            rest = rest.Substring(0, rest.Length - 3).Trim();
        string word = rest;

        int i = startIdx + 1;
        bool matched = false;

        while (i < endIdx)
        {
            string cur = lines[i].Trim();
            if (cur == "esac") { i++; break; }

            // Look for pattern)
            int parenIdx = cur.IndexOf(')');
            if (parenIdx > 0)
            {
                string pattern = cur.Substring(0, parenIdx).Trim();
                string cmdPart = cur.Substring(parenIdx + 1).Trim();
                if (cmdPart.EndsWith(";;"))
                    cmdPart = cmdPart.Substring(0, cmdPart.Length - 2).Trim();

                bool isMatch = pattern == "*" || pattern == word ||
                    (pattern.StartsWith("\"") && pattern.EndsWith("\"") &&
                     pattern.Substring(1, pattern.Length - 2) == word);

                if (!matched && isMatch)
                {
                    matched = true;
                    if (cmdPart.Length > 0)
                    {
                        string expanded = ExpandScriptVars(cmdPart, vars);
                        expanded = ExpandCommandSubstitutions(expanded, vars, label);
                        try { ExecuteLine(expanded); vars["?"] = "0"; }
                        catch { vars["?"] = "1"; }
                    }

                    // Execute subsequent lines until ;;
                    i++;
                    while (i < endIdx)
                    {
                        string cl = lines[i].Trim();
                        if (cl == ";;" || cl == "esac") break;
                        string expanded = ExpandScriptVars(cl, vars);
                        expanded = ExpandCommandSubstitutions(expanded, vars, label);
                        try { ExecuteLine(expanded); vars["?"] = "0"; }
                        catch { vars["?"] = "1"; }
                        i++;
                    }
                    if (i < endIdx && lines[i].Trim() == ";;") i++;
                    continue;
                }
            }
            i++;
        }

        return i;
    }

    // ?? Condition evaluation ???????????????????????????????????????

    /// <summary>
    /// Evaluate a shell condition. Supports:
    ///   [ -f path ]        file exists
    ///   [ -d path ]        directory exists
    ///   [ -z "str" ]       string is empty
    ///   [ -n "str" ]       string is non-empty
    ///   [ str1 = str2 ]    string equality
    ///   [ str1 != str2 ]   string inequality
    ///   [ num1 -eq num2 ]  numeric equal
    ///   [ num1 -ne num2 ]  numeric not equal
    ///   [ num1 -lt num2 ]  numeric less than
    ///   [ num1 -le num2 ]  numeric less or equal
    ///   [ num1 -gt num2 ]  numeric greater than
    ///   [ num1 -ge num2 ]  numeric greater or equal
    ///   ! CONDITION         negate
    ///   command             true if exit code 0
    /// </summary>
    private bool EvaluateCondition(string condition, Dictionary<string, string> vars, string label)
    {
        condition = condition.Trim();

        // Handle negation
        if (condition.StartsWith("! "))
            return !EvaluateCondition(condition.Substring(2).Trim(), vars, label);

        // Test brackets: [ ... ]
        if (condition.StartsWith("[") && condition.EndsWith("]"))
        {
            string inner = condition.Substring(1, condition.Length - 2).Trim();
            return EvaluateTest(inner, vars);
        }

        // [[ ... ]]
        if (condition.StartsWith("[[") && condition.EndsWith("]]"))
        {
            string inner = condition.Substring(2, condition.Length - 4).Trim();
            return EvaluateTest(inner, vars);
        }

        // test command
        if (condition.StartsWith("test "))
        {
            string inner = condition.Substring(5).Trim();
            return EvaluateTest(inner, vars);
        }

        // true/false literals
        if (condition == "true") return true;
        if (condition == "false") return false;

        // Run command, check exit code
        try
        {
            var origOut = Console.Out;
            Console.SetOut(new StringWriter()); // suppress output
            try
            {
                ExecuteLine(condition);
                vars["?"] = "0";
                return true;
            }
            catch
            {
                vars["?"] = "1";
                return false;
            }
            finally
            {
                Console.SetOut(origOut);
            }
        }
        catch
        {
            return false;
        }
    }

    private bool EvaluateTest(string expr, Dictionary<string, string> vars)
    {
        // Remove quotes from tokens
        var parts = TokenizeTest(expr);

        if (parts.Count == 0) return false;

        // Unary tests
        if (parts.Count == 2)
        {
            string op = parts[0];
            string val = Unquote(parts[1]);
            switch (op)
            {
                case "-f": return _fs.IsFile(VirtualFileSystem.ResolvePath(_cwd, val));
                case "-d": return _fs.IsDirectory(VirtualFileSystem.ResolvePath(_cwd, val));
                case "-e": return _fs.Exists(VirtualFileSystem.ResolvePath(_cwd, val));
                case "-z": return string.IsNullOrEmpty(val);
                case "-n": return !string.IsNullOrEmpty(val);
                case "!": return !EvaluateTest(parts[1], vars);
            }
        }

        // Binary tests
        if (parts.Count == 3)
        {
            string left = Unquote(parts[0]);
            string op = parts[1];
            string right = Unquote(parts[2]);

            switch (op)
            {
                case "=":
                case "==": return left == right;
                case "!=": return left != right;
                case "-eq": return ParseInt(left) == ParseInt(right);
                case "-ne": return ParseInt(left) != ParseInt(right);
                case "-lt": return ParseInt(left) < ParseInt(right);
                case "-le": return ParseInt(left) <= ParseInt(right);
                case "-gt": return ParseInt(left) > ParseInt(right);
                case "-ge": return ParseInt(left) >= ParseInt(right);
            }
        }

        // Negated binary: ! -f path
        if (parts.Count == 3 && parts[0] == "!")
        {
            return !EvaluateTest(string.Join(" ", parts.Skip(1)), vars);
        }

        // Single value: truthy if non-empty
        if (parts.Count == 1)
            return !string.IsNullOrEmpty(Unquote(parts[0]));

        return false;
    }

    private static List<string> TokenizeTest(string expr)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;
        char qChar = '"';

        foreach (char c in expr)
        {
            if (inQuote)
            {
                if (c == qChar) { inQuote = false; sb.Append(c); }
                else sb.Append(c);
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = true; qChar = c; sb.Append(c);
            }
            else if (c == ' ')
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
            }
            else sb.Append(c);
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    private static string Unquote(string s)
    {
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
            return s[1..^1];
        return s;
    }

    private static int ParseInt(string s)
    {
        return int.TryParse(s, out int v) ? v : 0;
    }

    /// <summary>
    /// Evaluate simple arithmetic expressions: +, -, *, /, %
    /// Supports integer math only.
    /// </summary>
    private static string EvaluateArithmetic(string expr)
    {
        try
        {
            // Tokenize: split on operators while keeping them
            var tokens = new List<string>();
            var num = new StringBuilder();
            foreach (char c in expr)
            {
                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '%')
                {
                    if (num.Length > 0) { tokens.Add(num.ToString().Trim()); num.Clear(); }
                    tokens.Add(c.ToString());
                }
                else
                {
                    num.Append(c);
                }
            }
            if (num.Length > 0) tokens.Add(num.ToString().Trim());

            if (tokens.Count == 0) return "0";

            // Simple left-to-right evaluation (no operator precedence beyond * / %)
            // First pass: handle *, /, %
            var pass1 = new List<string>();
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i] == "*" || tokens[i] == "/" || tokens[i] == "%")
                {
                    int left = ParseInt(pass1[pass1.Count - 1]);
                    int right = ParseInt(tokens[++i]);
                    int result = tokens[i - 1] switch
                    {
                        "*" => left * right,
                        "/" => right != 0 ? left / right : 0,
                        "%" => right != 0 ? left % right : 0,
                        _ => 0
                    };
                    pass1[pass1.Count - 1] = result.ToString();
                }
                else
                {
                    pass1.Add(tokens[i]);
                }
            }

            // Second pass: handle +, -
            int total = ParseInt(pass1[0]);
            for (int i = 1; i + 1 < pass1.Count; i += 2)
            {
                int right = ParseInt(pass1[i + 1]);
                if (pass1[i] == "+") total += right;
                else if (pass1[i] == "-") total -= right;
            }

            return total.ToString();
        }
        catch
        {
            return "0";
        }
    }

    // ?? Variable expansion ?????????????????????????????????????????

    private string ExpandScriptVars(string line, Dictionary<string, string> vars)
    {
        // First expand built-in shell vars
        line = ExpandVariables(line);

        // Then expand script-local $VAR and ${VAR}
        var sb = new StringBuilder();
        int i = 0;
        while (i < line.Length)
        {
            if (line[i] == '$' && i + 1 < line.Length)
            {
                if (line[i + 1] == '{')
                {
                    int close = line.IndexOf('}', i + 2);
                    if (close > 0)
                    {
                        string name = line.Substring(i + 2, close - i - 2);
                        sb.Append(vars.GetValueOrDefault(name, ""));
                        i = close + 1;
                        continue;
                    }
                }
                else if (line[i + 1] == '(')
                {
                    // Arithmetic expansion: $((expr))
                    if (i + 2 < line.Length && line[i + 2] == '(')
                    {
                        int closeIdx = line.IndexOf("))", i + 3);
                        if (closeIdx > 0)
                        {
                            string expr = line.Substring(i + 3, closeIdx - i - 3).Trim();
                            sb.Append(EvaluateArithmetic(expr));
                            i = closeIdx + 2;
                            continue;
                        }
                    }
                    // Command substitution handled later
                    sb.Append(line[i]);
                    i++;
                    continue;
                }
                else
                {
                    // $VAR or $1 etc
                    int start = i + 1;
                    int end = start;
                    while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_' || line[end] == '?' || line[end] == '#' || line[end] == '@'))
                        end++;
                    if (end > start)
                    {
                        string name = line.Substring(start, end - start);
                        sb.Append(vars.GetValueOrDefault(name, ""));
                        i = end;
                        continue;
                    }
                }
            }
            sb.Append(line[i]);
            i++;
        }
        return sb.ToString();
    }

    // ?? Command substitution: $(cmd) ???????????????????????????????

    private string ExpandCommandSubstitutions(string line, Dictionary<string, string> vars, string label)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < line.Length)
        {
            if (i + 1 < line.Length && line[i] == '$' && line[i + 1] == '(')
            {
                // Find matching close paren
                int depth = 1;
                int start = i + 2;
                int end = start;
                while (end < line.Length && depth > 0)
                {
                    if (line[end] == '(') depth++;
                    else if (line[end] == ')') depth--;
                    if (depth > 0) end++;
                }

                string cmd = line.Substring(start, end - start);
                cmd = ExpandScriptVars(cmd, vars);

                // Capture command output
                var origOut = Console.Out;
                var capture = new StringWriter();
                Console.SetOut(capture);
                try
                {
                    ExecuteLine(cmd);
                    vars["?"] = "0";
                }
                catch
                {
                    vars["?"] = "1";
                }
                finally
                {
                    Console.SetOut(origOut);
                }

                string result = capture.ToString().TrimEnd('\n', '\r');
                sb.Append(result);
                i = end + 1;
                continue;
            }

            // Backtick form: `cmd`
            if (line[i] == '`')
            {
                int closeIdx = line.IndexOf('`', i + 1);
                if (closeIdx > i)
                {
                    string cmd = line.Substring(i + 1, closeIdx - i - 1);
                    cmd = ExpandScriptVars(cmd, vars);

                    var origOut = Console.Out;
                    var capture = new StringWriter();
                    Console.SetOut(capture);
                    try { ExecuteLine(cmd); vars["?"] = "0"; }
                    catch { vars["?"] = "1"; }
                    finally { Console.SetOut(origOut); }

                    sb.Append(capture.ToString().TrimEnd('\n', '\r'));
                    i = closeIdx + 1;
                    continue;
                }
            }

            sb.Append(line[i]);
            i++;
        }
        return sb.ToString();
    }

    // ?? Helper: check if line is VAR=value ?????????????????????????

    private static bool IsAssignment(string line, out string key, out string value)
    {
        key = value = "";
        // Must not start with a command keyword
        if (line.StartsWith("if ") || line.StartsWith("for ") || line.StartsWith("while ") ||
            line.StartsWith("case ") || line.StartsWith("echo ") || line.StartsWith("export "))
            return false;

        int eq = line.IndexOf('=');
        if (eq <= 0) return false;

        string left = line.Substring(0, eq);
        // Variable name must be alphanumeric/underscore, no spaces
        foreach (char c in left)
            if (!char.IsLetterOrDigit(c) && c != '_') return false;

        key = left;
        value = Unquote(line.Substring(eq + 1).Trim());
        return true;
    }

    // ?? Helper: extract condition from "if CONDITION" ??????????????

    private static string ExtractCondition(string line, string keyword)
    {
        return line.Substring(keyword.Length).Trim();
    }

    // ?? Helper: find block terminator at the same nesting level ????

    private static int FindBlockEnd(string[] lines, int start, int end, string[] terminators)
    {
        int depth = 0;
        for (int i = start; i < end; i++)
        {
            string cur = lines[i].Trim();
            // Track nesting
            if (cur.StartsWith("if ")) depth++;
            if (cur == "fi") { if (depth > 0) { depth--; continue; } }

            if (depth == 0)
            {
                foreach (var t in terminators)
                {
                    if (cur == t || cur.StartsWith(t + " "))
                        return i;
                }
            }
        }
        return end;
    }

    /// <summary>
    /// Find the matching close keyword for a nested block (for/while do..done).
    /// </summary>
    private static int FindNestedBlockEnd(string[] lines, int start, int end,
        string openKeyword, string closeKeyword)
    {
        int depth = 0;
        for (int i = start; i < end; i++)
        {
            string cur = lines[i].Trim();
            // Count nested for/while blocks
            if (cur.StartsWith("for ") || cur.StartsWith("while "))
                depth++;
            if (cur == closeKeyword || cur.StartsWith(closeKeyword + " ") ||
                cur.StartsWith(closeKeyword + ";"))
            {
                if (depth > 0) { depth--; continue; }
                return i;
            }
        }
        return end;
    }

    // ?? Helper: parse for-loop list items ??????????????????????????

    private List<string> ParseForList(string listPart, Dictionary<string, string> vars, string label)
    {
        listPart = ExpandCommandSubstitutions(listPart, vars, label);
        var items = new List<string>();
        var sb = new StringBuilder();
        bool inQuote = false;
        char qChar = '"';

        foreach (char c in listPart)
        {
            if (inQuote)
            {
                if (c == qChar) inQuote = false;
                else sb.Append(c);
            }
            else if (c == '"' || c == '\'')
            {
                inQuote = true; qChar = c;
            }
            else if (c == ' ')
            {
                if (sb.Length > 0) { items.Add(sb.ToString()); sb.Clear(); }
            }
            else sb.Append(c);
        }
        if (sb.Length > 0) items.Add(sb.ToString());
        return items;
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

    // ?? Daemon management ????????????????????????????????????????????

    private void CmdDaemon(List<string> args)
    {
        if (args.Count == 0)
        {
            PrintDaemonUsage();
            return;
        }

        string sub = args[0].ToLowerInvariant();
        var subArgs = args.Skip(1).ToList();

        switch (sub)
        {
            case "start":
                DaemonStart(subArgs);
                break;
            case "stop":
                DaemonStop(subArgs);
                break;
            case "list":
            case "ls":
                DaemonList();
                break;
            case "status":
                DaemonStatus(subArgs);
                break;
            case "-h":
            case "--help":
                PrintDaemonUsage();
                break;
            default:
                Console.WriteLine($"daemon: unknown subcommand '{sub}'");
                PrintDaemonUsage();
                break;
        }
    }

    private void DaemonStart(List<string> subArgs)
    {
        if (_currentUser.Uid != 0)
        {
            Console.WriteLine("daemon: permission denied (must be root)");
            return;
        }

        if (subArgs.Count == 0)
        {
            Console.WriteLine("daemon: start requires a script path");
            Console.WriteLine("Usage: daemon start <script.cs> [args...]");
            return;
        }

        string scriptArg = subArgs[0];
        string vfsPath = VirtualFileSystem.ResolvePath(_cwd, scriptArg);

        // Try adding .cs if not found
        if (!_fs.IsFile(vfsPath) && !vfsPath.EndsWith(".cs"))
            vfsPath += ".cs";

        // Also search /sbin, /bin
        if (!_fs.IsFile(vfsPath))
        {
            foreach (var dir in new[] { "/sbin", "/bin", "/usr/local/bin" })
            {
                string candidate = dir + "/" + scriptArg + ".cs";
                if (_fs.IsFile(candidate)) { vfsPath = candidate; break; }
                candidate = dir + "/" + scriptArg;
                if (_fs.IsFile(candidate)) { vfsPath = candidate; break; }
            }
        }

        string name = VirtualFileSystem.GetName(vfsPath).Replace(".cs", "");
        var extraArgs = subArgs.Skip(1).ToArray();

        int pid = _daemonMgr.Start(name, vfsPath, extraArgs, _currentUser, _cwd);
        if (pid >= 0)
        {
            Console.WriteLine($"daemon: started '{name}' (pid {pid})");
        }
    }

    private void DaemonStop(List<string> subArgs)
    {
        if (_currentUser.Uid != 0)
        {
            Console.WriteLine("daemon: permission denied (must be root)");
            return;
        }

        if (subArgs.Count == 0)
        {
            Console.WriteLine("daemon: stop requires a name or PID");
            Console.WriteLine("Usage: daemon stop <name|pid>");
            return;
        }

        if (_daemonMgr.Stop(subArgs[0], _currentUser.Uid))
        {
            Console.WriteLine($"daemon: '{subArgs[0]}' stopped");
        }
    }

    private void DaemonList()
    {
        var daemons = _daemonMgr.List();
        if (daemons.Length == 0)
        {
            Console.WriteLine("No daemons running.");
            return;
        }

        Console.WriteLine($"{"PID",-8} {"NAME",-16} {"STATUS",-10} {"OWNER",-10} {"STARTED"}");
        foreach (var d in daemons)
        {
            Console.WriteLine($"{d.Pid,-8} {d.Name,-16} {d.Status,-10} {d.Owner,-10} {d.StartedAt:HH:mm:ss}");
        }
    }

    private void DaemonStatus(List<string> subArgs)
    {
        if (subArgs.Count == 0)
        {
            DaemonList();
            return;
        }

        var info = _daemonMgr.GetStatus(subArgs[0]);
        if (info == null)
        {
            Console.WriteLine($"daemon: '{subArgs[0]}' not found");
            return;
        }

        Console.WriteLine($"Name:    {info.Name}");
        Console.WriteLine($"PID:     {info.Pid}");
        Console.WriteLine($"Status:  {info.Status}");
        Console.WriteLine($"Script:  {info.ScriptPath}");
        Console.WriteLine($"Owner:   {info.Owner}");
        Console.WriteLine($"Started: {info.StartedAt:yyyy-MM-dd HH:mm:ss}");
        if (info.StoppedAt.HasValue)
            Console.WriteLine($"Stopped: {info.StoppedAt.Value:yyyy-MM-dd HH:mm:ss}");
    }

    private static void PrintDaemonUsage()
    {
        Console.WriteLine("daemon — manage background daemon processes");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  daemon start <script.cs> [args...]   Start a daemon");
        Console.WriteLine("  daemon stop <name|pid>               Stop a running daemon");
        Console.WriteLine("  daemon list                          List all daemons");
        Console.WriteLine("  daemon status <name|pid>             Show daemon details");
        Console.WriteLine();
        Console.WriteLine("Daemon scripts must implement:");
        Console.WriteLine("  static int Daemon(NixApi api, string[] args)");
        Console.WriteLine();
        Console.WriteLine("Only root can start or stop daemons.");
    }
}
