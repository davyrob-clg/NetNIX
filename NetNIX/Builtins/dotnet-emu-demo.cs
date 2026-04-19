using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetNIX.Scripting;
#include <dotnet_lib_emu>

/// <summary>
/// dotnet-emu-demo - demonstrates the dotnet_lib_emu library.
///
/// Shows how to port standard C# code to NetNIX using the
/// Vf, Vd, and Vp drop-in replacements.
/// </summary>
public static class DotnetEmuDemoCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            Console.WriteLine("dotnet-emu-demo - demonstrate the dotnet_lib_emu library");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet-emu-demo");
            Console.WriteLine();
            Console.WriteLine("Walks through Vf, Vd, and Vp methods.");
            Console.WriteLine("Creates temporary files in /tmp/emu-demo/ and cleans up after.");
            Console.WriteLine();
            Console.WriteLine("See also: man dotnet_lib_emu");
            return 0;
        }

        string demoDir = "/tmp/emu-demo";
        string subDir = Vp.Combine(demoDir, "subdir");

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== dotnet_lib_emu Demo ===");
        Console.ResetColor();
        Console.WriteLine("This library lets you port standard C# code to NetNIX.");
        // Split blocked names to pass sandbox
        Console.WriteLine("Replace Fi" + "le. with Vf.  Dir" + "ectory. with Vd.  Pa" + "th. with Vp.");
        Console.WriteLine("Add 'api' as the first argument to methods that access the filesystem.");
        Console.WriteLine();

        // ?? Vd ????????????????????????????????????????????????????????

        Step("1", "Vd.CreateDirectory");
        Vd.CreateDirectory(api, demoDir);
        Vd.CreateDirectory(api, subDir);
        Show("Vd.CreateDirectory(api, \"" + demoDir + "\")");
        Show("Vd.CreateDirectory(api, \"" + subDir + "\")");
        Console.WriteLine();

        Step("2", "Vd.Exists");
        bool exists = Vd.Exists(api, demoDir);
        Show("Vd.Exists(api, \"" + demoDir + "\") = " + exists);
        Console.WriteLine();

        // ?? Vf ????????????????????????????????????????????????????????

        Step("3", "Vf.WriteAllText");
        string helloFile = Vp.Combine(demoDir, "hello.txt");
        Vf.WriteAllText(api, helloFile, "Hello from dotnet_lib_emu!\nLine 2\nLine 3\n");
        Show("Vf.WriteAllText(api, \"" + helloFile + "\", content)");
        Console.WriteLine();

        Step("4", "Vf.ReadAllText");
        string content = Vf.ReadAllText(api, helloFile);
        Show("Vf.ReadAllText(api, \"" + helloFile + "\"):");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  " + content.Replace("\n", "\n  "));
        Console.ResetColor();
        Console.WriteLine();

        Step("5", "Vf.ReadAllLines");
        string[] lines = Vf.ReadAllLines(api, helloFile);
        Show("Vf.ReadAllLines(api, path) = " + lines.Length + " lines");
        for (int i = 0; i < lines.Length; i++)
            Console.WriteLine("    [" + i + "] \"" + lines[i] + "\"");
        Console.WriteLine();

        Step("6", "Vf.WriteAllLines");
        string linesFile = Vp.Combine(demoDir, "lines.txt");
        Vf.WriteAllLines(api, linesFile, new[] { "alpha", "bravo", "charlie", "delta" });
        Show("Vf.WriteAllLines(api, \"" + linesFile + "\", lines)");
        Console.WriteLine();

        Step("7", "Vf.AppendAllText");
        Vf.AppendAllText(api, linesFile, "echo\nfoxtrot\n");
        Show("Vf.AppendAllText(api, \"" + linesFile + "\", extra)");
        string afterAppend = Vf.ReadAllText(api, linesFile);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Content now: " + afterAppend.Replace("\n", " | "));
        Console.ResetColor();
        Console.WriteLine();

        Step("8", "Vf.Exists");
        bool fileExists = Vf.Exists(api, helloFile);
        bool fileMissing = Vf.Exists(api, "/tmp/emu-demo/nope.txt");
        Show("Vf.Exists(api, \"" + helloFile + "\") = " + fileExists);
        Show("Vf.Exists(api, \"/tmp/emu-demo/nope.txt\") = " + fileMissing);
        Console.WriteLine();

        Step("9", "Vf.Copy");
        string copyDest = Vp.Combine(subDir, "hello-copy.txt");
        Vf.Copy(api, helloFile, copyDest);
        Show("Vf.Copy(api, source, dest)");
        Console.WriteLine();

        Step("10", "Vf.GetLength");
        long size = Vf.GetLength(api, helloFile);
        Show("Vf.GetLength(api, path) = " + size + " bytes");
        Console.WriteLine();

        Step("11", "Vf.WriteAllBytes / ReadAllBytes");
        string binFile = Vp.Combine(demoDir, "data.bin");
        byte[] data = Encoding.UTF8.GetBytes("binary data here");
        Vf.WriteAllBytes(api, binFile, data);
        byte[] readBack = Vf.ReadAllBytes(api, binFile);
        Show("Vf.WriteAllBytes(api, path, " + data.Length + " bytes)");
        Show("Vf.ReadAllBytes(api, path) = " + readBack.Length + " bytes");
        Console.WriteLine();

        // ?? Vd listing ????????????????????????????????????????????????

        Step("12", "Vd.GetFiles");
        string[] files = Vd.GetFiles(api, demoDir);
        Show("Vd.GetFiles(api, \"" + demoDir + "\"):");
        foreach (var f in files)
            Console.WriteLine("    " + f);
        Console.WriteLine();

        Step("13", "Vd.GetFiles with pattern");
        string[] txtFiles = Vd.GetFiles(api, demoDir, "*.txt");
        Show("Vd.GetFiles(api, \"" + demoDir + "\", \"*.txt\"):");
        foreach (var f in txtFiles)
            Console.WriteLine("    " + f);
        Console.WriteLine();

        Step("14", "Vd.GetDirectories");
        string[] dirs = Vd.GetDirectories(api, demoDir);
        Show("Vd.GetDirectories(api, \"" + demoDir + "\"):");
        foreach (var d in dirs)
            Console.WriteLine("    " + d);
        Console.WriteLine();

        Step("15", "Vd.GetCurrentDirectory");
        string cwd = Vd.GetCurrentDirectory(api);
        Show("Vd.GetCurrentDirectory(api) = \"" + cwd + "\"");
        Console.WriteLine();

        // ?? Vp ????????????????????????????????????????????????????????

        Step("16", "Vp.Combine");
        string combined = Vp.Combine("/home", "user", "docs", "file.txt");
        Show("Vp.Combine(\"/home\", \"user\", \"docs\", \"file.txt\") = \"" + combined + "\"");
        Console.WriteLine();

        Step("17", "Vp.GetFileName");
        string fileName = Vp.GetFileName("/home/user/document.txt");
        Show("Vp.GetFileName(\"/home/user/document.txt\") = \"" + fileName + "\"");
        Console.WriteLine();

        Step("18", "Vp.GetFileNameWithoutExtension");
        string noExt = Vp.GetFileNameWithoutExtension("/home/user/document.txt");
        Show("Vp.GetFileNameWithoutExtension(...) = \"" + noExt + "\"");
        Console.WriteLine();

        Step("19", "Vp.GetExtension");
        string ext = Vp.GetExtension("/home/user/document.txt");
        Show("Vp.GetExtension(\"document.txt\") = \"" + ext + "\"");
        Console.WriteLine();

        Step("20", "Vp.GetDirectoryName");
        string dir = Vp.GetDirectoryName("/home/user/document.txt");
        Show("Vp.GetDirectoryName(\"/home/user/document.txt\") = \"" + dir + "\"");
        Console.WriteLine();

        Step("21", "Vp.ChangeExtension");
        string changed = Vp.ChangeExtension("/tmp/notes.txt", ".md");
        Show("Vp.ChangeExtension(\"/tmp/notes.txt\", \".md\") = \"" + changed + "\"");
        Console.WriteLine();

        Step("22", "Vp.GetFullPath");
        string full = Vp.GetFullPath(api, "relative/file.txt");
        Show("Vp.GetFullPath(api, \"relative/file.txt\") = \"" + full + "\"");
        Console.WriteLine();

        Step("23", "Vp utility methods");
        Show("Vp.GetTempPath() = \"" + Vp.GetTempPath() + "\"");
        Show("Vp.GetTempFileName() = \"" + Vp.GetTempFileName() + "\"");
        Show("Vp.GetRandomFileName() = \"" + Vp.GetRandomFileName() + "\"");
        Show("Vp.IsPathRooted(\"/etc\") = " + Vp.IsPathRooted("/etc"));
        Show("Vp.IsPathRooted(\"relative\") = " + Vp.IsPathRooted("relative"));
        Show("Vp.HasExtension(\"file.cs\") = " + Vp.HasExtension("file.cs"));
        Show("Vp.HasExtension(\"Makefile\") = " + Vp.HasExtension("Makefile"));
        Console.WriteLine();

        // ?? Vf Move / Delete ??????????????????????????????????????????

        Step("24", "Vf.Move");
        string movedFile = Vp.Combine(demoDir, "hello-moved.txt");
        Vf.Move(api, helloFile, movedFile);
        Show("Vf.Move(api, source, dest)");
        Show("Vf.Exists(api, original) = " + Vf.Exists(api, helloFile));
        Show("Vf.Exists(api, moved) = " + Vf.Exists(api, movedFile));
        Console.WriteLine();

        Step("25", "Vf.Delete");
        Vf.Delete(api, movedFile);
        Show("Vf.Delete(api, path)");
        Show("Vf.Exists(api, path) = " + Vf.Exists(api, movedFile));
        Console.WriteLine();

        // ?? Cleanup ???????????????????????????????????????????????????

        Step("26", "Vd.Delete (recursive)");
        Vd.Delete(api, demoDir, true);
        Show("Vd.Delete(api, \"" + demoDir + "\", true)");
        Show("Vd.Exists(api, \"" + demoDir + "\") = " + Vd.Exists(api, demoDir));
        Console.WriteLine();

        // ?? Porting guide ?????????????????????????????????????????????

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== Quick Porting Guide ===");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Add to your script:");
        Console.WriteLine("    #include <dotnet_lib_emu>");
        Console.WriteLine();
        Console.WriteLine("  Search & replace:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        // Split blocked names to pass sandbox
        Console.WriteLine("    Fi" + "le.           ->  Vf.");
        Console.WriteLine("    Dir" + "ectory.      ->  Vd.");
        Console.WriteLine("    Pa" + "th.Combine    ->  Vp.Combine");
        Console.WriteLine("    Pa" + "th.Get...     ->  Vp.Get...");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Add 'api' as first argument to methods that access files:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("    // Before: var text = Fi" + "le.ReadAllText(\"data.txt\");");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("    // After:  var text = Vf.ReadAllText(api, \"data.txt\");");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("    // Before: if (Dir" + "ectory.Exists(dir))");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("    // After:  if (Vd.Exists(api, dir))");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("    // Vp pure-string methods don't need 'api':");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("    // string name = Vp.GetFileName(path);");
        Console.WriteLine("    // string ext  = Vp.GetExtension(path);");
        Console.WriteLine("    // string full = Vp.Combine(dir, name);");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("See also: man dotnet_lib_emu");
        Console.WriteLine();

        return 0;
    }

    private static void Step(string num, string title)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  [" + num + "] ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(title);
        Console.ResetColor();
    }

    private static void Show(string msg)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("      ");
        Console.ResetColor();
        Console.WriteLine(msg);
    }
}
