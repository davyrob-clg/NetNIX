/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
namespace NetNIX.Setup;

/// <summary>
/// Loads built-in command scripts from the Builtins/ and SystemBuiltins/
/// directories that ship alongside the NetNIX binary. These .cs files are
/// not compiled into the assembly — they are plain-text content files copied
/// to the output directory.
///
/// Builtins/        ? installed to /bin/  (user commands)
/// SystemBuiltins/  ? installed to /sbin/ (root/admin commands)
///
/// To add or remove commands, simply drop .cs files into the appropriate
/// directory — no code changes required.
/// </summary>
public static class BuiltinScripts
{
    private static readonly string BinDir =
        Path.Combine(AppContext.BaseDirectory, "Builtins");

    private static readonly string SbinDir =
        Path.Combine(AppContext.BaseDirectory, "SystemBuiltins");

    /// <summary>
    /// Returns a dictionary mapping VFS install paths to the source code
    /// read from the on-disk directories.
    /// Builtins/*.cs   ? /bin/*.cs
    /// SystemBuiltins/*.cs ? /sbin/*.cs
    /// </summary>
    public static Dictionary<string, string> LoadAll()
    {
        var scripts = new Dictionary<string, string>();

        LoadFromDirectory(scripts, BinDir, "/bin/");
        LoadFromDirectory(scripts, SbinDir, "/sbin/");

        return scripts;
    }

    /// <summary>
    /// Reads a single builtin script by name, searching /bin first then /sbin.
    /// Returns null if the file is not found.
    /// </summary>
    public static string? Load(string name)
    {
        string binPath = Path.Combine(BinDir, name + ".cs");
        if (File.Exists(binPath))
            return File.ReadAllText(binPath);

        string sbinPath = Path.Combine(SbinDir, name + ".cs");
        if (File.Exists(sbinPath))
            return File.ReadAllText(sbinPath);

        return null;
    }

    private static void LoadFromDirectory(Dictionary<string, string> scripts, string hostDir, string vfsPrefix)
    {
        if (!Directory.Exists(hostDir))
            return;

        foreach (var file in Directory.GetFiles(hostDir, "*.cs"))
        {
            string filename = Path.GetFileName(file);
            string vfsPath = vfsPrefix + filename;
            string source = File.ReadAllText(file);
            scripts[vfsPath] = source;
        }
    }
}
