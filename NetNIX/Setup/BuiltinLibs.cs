/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
namespace NetNIX.Setup;

/// <summary>
/// Loads built-in library scripts from the Libs/ directory that ships
/// alongside the NetNIX binary. These .cs files are not compiled into the
/// assembly — they are plain-text content files copied to the output directory
/// and installed into /lib/ in the virtual filesystem.
/// </summary>
public static class BuiltinLibs
{
    private static readonly string LibsDir =
        Path.Combine(AppContext.BaseDirectory, "Libs");

    /// <summary>
    /// Returns a dictionary mapping VFS install paths (/lib/name.cs) to
    /// the source code read from the on-disk Libs/ directory.
    /// </summary>
    public static Dictionary<string, string> LoadAll()
    {
        var libs = new Dictionary<string, string>();

        if (!Directory.Exists(LibsDir))
            return libs;

        foreach (var file in Directory.GetFiles(LibsDir, "*.cs"))
        {
            string filename = Path.GetFileName(file);
            string vfsPath = "/lib/" + filename;
            string source = File.ReadAllText(file);
            libs[vfsPath] = source;
        }

        return libs;
    }

    /// <summary>
    /// Reads a single library by name (e.g. "demoapilib" reads Libs/demoapilib.cs).
    /// Returns null if the file is not found.
    /// </summary>
    public static string? Load(string name)
    {
        string path = Path.Combine(LibsDir, name + ".cs");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
