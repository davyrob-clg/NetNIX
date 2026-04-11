using System.Text;
using System.Text;
using NetNIX.VFS;

namespace NetNIX.Shell;

/// <summary>
/// A simple full-screen text editor inspired by nano.
/// Supports cursor movement, insert/delete, scrolling, save, and C# templates.
/// </summary>
public sealed class TextEditor
{
    private readonly VirtualFileSystem _fs;
    private readonly string _filePath;
    private readonly int _ownerUid;
    private readonly int _ownerGid;

    private List<StringBuilder> _lines = [new StringBuilder()];
    private int _cursorRow;    // cursor position in _lines
    private int _cursorCol;    // cursor column in current line
    private int _scrollOffset; // first visible line index
    private bool _modified;
    private bool _quit;
    private string _statusMessage = "";
    private DateTime _statusTime = DateTime.MinValue;

    // Editor area dimensions (updated each render)
    private int _editorRows;
    private int _editorCols;

    private const string EditorName = "nedit";
    private const int StatusTimeout = 4; // seconds
    private const int GutterWidth = 7; // "1234 | " = 4 digits + space + separator + space

    public TextEditor(VirtualFileSystem fs, string filePath, int ownerUid, int ownerGid)
    {
        _fs = fs;
        _filePath = filePath;
        _ownerUid = ownerUid;
        _ownerGid = ownerGid;
    }

    public void Run()
    {
        LoadFile();
        Console.CursorVisible = false;

        try
        {
            while (!_quit)
            {
                Render();
                ProcessKey();
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.Clear();
        }
    }

    // ?? File I/O ???????????????????????????????????????????????????

    private void LoadFile()
    {
        if (_fs.IsFile(_filePath))
        {
            var node = _fs.GetNode(_filePath);
            if (node != null && !node.CanRead(_ownerUid, _ownerGid))
                return; // permission denied — start with empty buffer

            string text = Encoding.UTF8.GetString(_fs.ReadFile(_filePath));
            var split = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            _lines = split.Select(s => new StringBuilder(s)).ToList();
            if (_lines.Count == 0) _lines.Add(new StringBuilder());
        }
    }

    private void SaveFile()
    {
        // Check write permission on existing file, or parent write for new file
        if (_fs.IsFile(_filePath))
        {
            var node = _fs.GetNode(_filePath);
            if (node != null && !node.CanWrite(_ownerUid, _ownerGid))
            {
                SetStatus("Permission denied — cannot save");
                return;
            }
        }
        else
        {
            string parent = VirtualFileSystem.GetParent(_filePath);
            var parentNode = _fs.GetNode(parent);
            if (parentNode != null && !parentNode.CanWrite(_ownerUid, _ownerGid))
            {
                SetStatus("Permission denied — cannot save");
                return;
            }
        }

        var sb = new StringBuilder();
        for (int i = 0; i < _lines.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(_lines[i]);
        }

        var data = Encoding.UTF8.GetBytes(sb.ToString());
        if (_fs.IsFile(_filePath))
            _fs.WriteFile(_filePath, data);
        else
            _fs.CreateFile(_filePath, _ownerUid, _ownerGid, data);

        _fs.Save();
        _modified = false;
        SetStatus($"Saved {_filePath} ({_lines.Count} lines, {data.Length} bytes)");
    }

    // ?? Rendering ??????????????????????????????????????????????????

    private void Render()
    {
        _editorRows = Console.WindowHeight - 3; // 1 title + 1 status + 1 shortcut bar
        _editorCols = Console.WindowWidth;
        if (_editorRows < 1) _editorRows = 1;

        // Ensure cursor is visible
        if (_cursorRow < _scrollOffset)
            _scrollOffset = _cursorRow;
        if (_cursorRow >= _scrollOffset + _editorRows)
            _scrollOffset = _cursorRow - _editorRows + 1;

        var buf = new StringBuilder();

        // Disable cursor flicker
        buf.Append("\x1b[?25l");
        buf.Append("\x1b[H"); // move to top-left

        // ?? Title bar ??????????????????????????????????????????????
        string title = $" {EditorName} — {VirtualFileSystem.GetName(_filePath)}{(_modified ? " [modified]" : "")}";
        string lineInfo = $"Ln {_cursorRow + 1}, Col {_cursorCol + 1} ";
        int pad = _editorCols - title.Length - lineInfo.Length;
        if (pad < 0) pad = 0;
        buf.Append("\x1b[7m"); // inverse video
        buf.Append(title);
        buf.Append(new string(' ', pad));
        buf.Append(lineInfo);
        buf.Append("\x1b[0m");
        buf.AppendLine();

        // ?? Editor lines ???????????????????????????????????????????
        for (int i = 0; i < _editorRows; i++)
        {
            int lineIdx = _scrollOffset + i;
            buf.Append("\x1b[K"); // clear line

            if (lineIdx < _lines.Count)
            {
                string lineText = _lines[lineIdx].ToString();
                // Show line number gutter
                string gutter = $"{lineIdx + 1,4} | ";
                buf.Append("\x1b[90m"); // dim
                buf.Append(gutter);
                buf.Append("\x1b[0m");

                int maxText = _editorCols - GutterWidth;
                if (lineText.Length > maxText)
                    buf.Append(lineText[..maxText]);
                else
                    buf.Append(lineText);
            }
            else
            {
                buf.Append("\x1b[90m   ~ \x1b[0m");
            }

            buf.AppendLine();
        }

        // ?? Status message ?????????????????????????????????????????
        buf.Append("\x1b[K");
        if ((DateTime.Now - _statusTime).TotalSeconds < StatusTimeout && _statusMessage.Length > 0)
        {
            buf.Append("\x1b[33m"); // yellow
            string msg = _statusMessage.Length > _editorCols ? _statusMessage[.._editorCols] : _statusMessage;
            buf.Append(msg);
            buf.Append("\x1b[0m");
        }
        buf.AppendLine();

        // ?? Shortcut bar ???????????????????????????????????????????
        buf.Append("\x1b[K");
        buf.Append("\x1b[7m"); // inverse
        string[] shortcuts =
        [
            "F2/^W Save",
            "^Q Quit",
            "^G Go to line",
            "^K Cut line",
            "^U Paste line",
            "^T Template",
        ];
        string bar = " " + string.Join("  ?  ", shortcuts) + " ";
        if (bar.Length > _editorCols)
            bar = bar[.._editorCols];
        else
            bar += new string(' ', _editorCols - bar.Length);
        buf.Append(bar);
        buf.Append("\x1b[0m");

        // ?? Position cursor ????????????????????????????????????????
        int screenRow = _cursorRow - _scrollOffset + 2; // +1 for title, +1 for 1-based
        int screenCol = _cursorCol + GutterWidth + 1;
        buf.Append($"\x1b[{screenRow};{screenCol}H");
        buf.Append("\x1b[?25h"); // show cursor

        Console.Write(buf.ToString());
    }

    // ?? Input handling ?????????????????????????????????????????????

    private void ProcessKey()
    {
        var key = Console.ReadKey(intercept: true);

        // Function key shortcuts
        switch (key.Key)
        {
            case ConsoleKey.F2: SaveFile(); return;
            case ConsoleKey.F10: HandleQuit(); return;
        }

        // Ctrl combinations
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
        {
            switch (key.Key)
            {
                case ConsoleKey.S: SaveFile(); return;   // may not work on Windows (XOFF)
                case ConsoleKey.W: SaveFile(); return;
                case ConsoleKey.Q: HandleQuit(); return;
                case ConsoleKey.G: HandleGoToLine(); return;
                case ConsoleKey.K: HandleCutLine(); return;
                case ConsoleKey.U: HandlePasteLine(); return;
                case ConsoleKey.T: HandleTemplate(); return;
            }
        }

        // Fallback: match by KeyChar for terminals that don't set Modifiers
        if (key.KeyChar < 32 && key.KeyChar > 0)
        {
            switch (key.KeyChar)
            {
                case '\x13': SaveFile(); return;   // Ctrl+S
                case '\x17': SaveFile(); return;   // Ctrl+W
                case '\x11': HandleQuit(); return;  // Ctrl+Q
                case '\x07': HandleGoToLine(); return; // Ctrl+G
                case '\x0B': HandleCutLine(); return;  // Ctrl+K
                case '\x15': HandlePasteLine(); return; // Ctrl+U
                case '\x14': HandleTemplate(); return;  // Ctrl+T
            }
        }

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                if (_cursorRow > 0)
                {
                    _cursorRow--;
                    ClampCol();
                }
                break;

            case ConsoleKey.DownArrow:
                if (_cursorRow < _lines.Count - 1)
                {
                    _cursorRow++;
                    ClampCol();
                }
                break;

            case ConsoleKey.LeftArrow:
                if (_cursorCol > 0)
                    _cursorCol--;
                else if (_cursorRow > 0)
                {
                    _cursorRow--;
                    _cursorCol = _lines[_cursorRow].Length;
                }
                break;

            case ConsoleKey.RightArrow:
                if (_cursorCol < _lines[_cursorRow].Length)
                    _cursorCol++;
                else if (_cursorRow < _lines.Count - 1)
                {
                    _cursorRow++;
                    _cursorCol = 0;
                }
                break;

            case ConsoleKey.Home:
                _cursorCol = 0;
                break;

            case ConsoleKey.End:
                _cursorCol = _lines[_cursorRow].Length;
                break;

            case ConsoleKey.PageUp:
                _cursorRow = Math.Max(0, _cursorRow - _editorRows);
                ClampCol();
                break;

            case ConsoleKey.PageDown:
                _cursorRow = Math.Min(_lines.Count - 1, _cursorRow + _editorRows);
                ClampCol();
                break;

            case ConsoleKey.Enter:
                InsertNewline();
                break;

            case ConsoleKey.Backspace:
                HandleBackspace();
                break;

            case ConsoleKey.Delete:
                HandleDelete();
                break;

            case ConsoleKey.Tab:
                InsertText("    "); // 4 spaces
                break;

            case ConsoleKey.Escape:
                // Do nothing — avoid inserting escape chars
                break;

            default:
                if (key.KeyChar >= 32 && key.KeyChar < 127)
                {
                    InsertChar(key.KeyChar);
                }
                break;
        }
    }

    // ?? Editing operations ?????????????????????????????????????????

    private void InsertChar(char c)
    {
        _lines[_cursorRow].Insert(_cursorCol, c);
        _cursorCol++;
        _modified = true;
    }

    private void InsertText(string text)
    {
        // Disable auto-indent during programmatic insertion
        // so templates/paste don't get corrupted by cascading indent.
        _suppressAutoIndent = true;
        try
        {
            foreach (char c in text)
            {
                if (c == '\n')
                    InsertNewline();
                else
                    InsertChar(c);
            }
        }
        finally
        {
            _suppressAutoIndent = false;
        }
    }

    private bool _suppressAutoIndent;

    private void InsertNewline()
    {
        string currentLine = _lines[_cursorRow].ToString();
        string before = currentLine[.._cursorCol];
        string after = currentLine[_cursorCol..];

        // Auto-indent: copy leading whitespace from current line (unless suppressed)
        string indent = "";
        if (!_suppressAutoIndent)
        {
            foreach (char ch in before)
            {
                if (ch == ' ') indent += " ";
                else break;
            }
        }

        _lines[_cursorRow] = new StringBuilder(before);
        _lines.Insert(_cursorRow + 1, new StringBuilder(indent + after));
        _cursorRow++;
        _cursorCol = indent.Length;
        _modified = true;
    }

    private void HandleBackspace()
    {
        if (_cursorCol > 0)
        {
            _lines[_cursorRow].Remove(_cursorCol - 1, 1);
            _cursorCol--;
            _modified = true;
        }
        else if (_cursorRow > 0)
        {
            // Merge with previous line
            int prevLen = _lines[_cursorRow - 1].Length;
            _lines[_cursorRow - 1].Append(_lines[_cursorRow]);
            _lines.RemoveAt(_cursorRow);
            _cursorRow--;
            _cursorCol = prevLen;
            _modified = true;
        }
    }

    private void HandleDelete()
    {
        if (_cursorCol < _lines[_cursorRow].Length)
        {
            _lines[_cursorRow].Remove(_cursorCol, 1);
            _modified = true;
        }
        else if (_cursorRow < _lines.Count - 1)
        {
            // Merge next line into current
            _lines[_cursorRow].Append(_lines[_cursorRow + 1]);
            _lines.RemoveAt(_cursorRow + 1);
            _modified = true;
        }
    }

    // ?? Cut / Paste ????????????????????????????????????????????????

    private readonly List<string> _clipboard = [];

    private void HandleCutLine()
    {
        _clipboard.Add(_lines[_cursorRow].ToString());
        if (_lines.Count > 1)
        {
            _lines.RemoveAt(_cursorRow);
            if (_cursorRow >= _lines.Count)
                _cursorRow = _lines.Count - 1;
        }
        else
        {
            _lines[0].Clear();
        }
        ClampCol();
        _modified = true;
        SetStatus("Line cut to clipboard");
    }

    private void HandlePasteLine()
    {
        if (_clipboard.Count == 0)
        {
            SetStatus("Clipboard is empty");
            return;
        }

        string last = _clipboard[^1];
        _lines.Insert(_cursorRow + 1, new StringBuilder(last));
        _cursorRow++;
        _cursorCol = 0;
        _modified = true;
        SetStatus("Line pasted");
    }

    // ?? Go to line ?????????????????????????????????????????????????

    private void HandleGoToLine()
    {
        string? input = Prompt("Go to line: ");
        if (input != null && int.TryParse(input, out int lineNum))
        {
            lineNum = Math.Clamp(lineNum, 1, _lines.Count);
            _cursorRow = lineNum - 1;
            _cursorCol = 0;
        }
    }

    // ?? Quit ???????????????????????????????????????????????????????

    private void HandleQuit()
    {
        if (_modified)
        {
            string? response = Prompt("Unsaved changes! Save before exit? (y/n/cancel): ");
            if (response == null || response.StartsWith("c", StringComparison.OrdinalIgnoreCase))
                return;
            if (response.StartsWith("y", StringComparison.OrdinalIgnoreCase))
                SaveFile();
        }
        _quit = true;
    }

    // ?? C# Templates ???????????????????????????????????????????????

    private void HandleTemplate()
    {
        string? choice = Prompt("Template: (1) Script  (2) Class  (3) Main  (4) Snippet: ");
        if (choice == null) return;

        string template = choice switch
        {
            "1" => ScriptTemplate(),
            "2" => ClassTemplate(),
            "3" => MainTemplate(),
            "4" => SnippetTemplate(),
            _ => ""
        };

        if (template.Length == 0)
        {
            SetStatus("Unknown template");
            return;
        }

        InsertText(template);
        SetStatus("Template inserted");
    }

    private static string ScriptTemplate()
    {
        return """
            using System;
            using System.Linq;
            using NetNIX.Scripting;

            public static class MyCommand
            {
                public static int Run(NixApi api, string[] args)
                {
                    // Your code here
                    Console.WriteLine("Hello from script!");
                    return 0;
                }
            }

            """; // raw string auto-strips indent based on closing position
    }

    private static string ClassTemplate()
    {
        return """
            using System;

            public class MyClass
            {
                public string Name { get; set; }

                public MyClass(string name)
                {
                    Name = name;
                }

                public override string ToString() => Name;
            }

            """;
    }

    private static string MainTemplate()
    {
        return """
            using System;
            using System.Linq;
            using NetNIX.Scripting;

            public static class Program
            {
                public static int Run(NixApi api, string[] args)
                {
                    if (args.Length == 0)
                    {
                        Console.WriteLine("Usage: <command> [args...]");
                        return 1;
                    }

                    foreach (var arg in args)
                        Console.WriteLine(arg);

                    return 0;
                }
            }

            """;
    }

    private static string SnippetTemplate()
    {
        return """
                    // Read a file
                    string content = api.ReadText("myfile.txt");

                    // Write a file
                    api.WriteText("output.txt", "Hello World\n");

                    // List directory
                    foreach (var path in api.ListDirectory("."))
                        Console.WriteLine(api.NodeName(path));

                    // Check permissions
                    if (api.CanRead("somefile"))
                        Console.WriteLine(api.ReadText("somefile"));

                    // Save filesystem
                    api.Save();

            """;
    }

    // ?? Helpers ????????????????????????????????????????????????????

    private void ClampCol()
    {
        if (_cursorCol > _lines[_cursorRow].Length)
            _cursorCol = _lines[_cursorRow].Length;
    }

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusTime = DateTime.Now;
    }

    private string? Prompt(string message)
    {
        // Draw prompt on the status line
        int promptRow = Console.WindowHeight - 2;
        Console.SetCursorPosition(0, promptRow);
        Console.Write("\x1b[K"); // clear line
        Console.Write("\x1b[33m" + message + "\x1b[0m");
        Console.CursorVisible = true;

        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape)
            {
                Console.CursorVisible = false;
                return null;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                Console.CursorVisible = false;
                return sb.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Remove(sb.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else if (key.KeyChar >= 32)
            {
                sb.Append(key.KeyChar);
                Console.Write(key.KeyChar);
            }
        }
    }
}
