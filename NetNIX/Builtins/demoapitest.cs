using System;
using System;
using System.Linq;
using NetNIX.Scripting;

#include <demoapilib>

/// <summary>
/// demoapitest — Demonstrates using a library (#include) in a NetNIX script.
///
/// This command uses DemoApiLib from /lib/demoapilib.cs to show how
/// scripts can share code through the #include directive.
///
/// Usage: demoapitest
/// </summary>
public static class DemoApiTestCommand
{
    public static int Run(NixApi api, string[] args)
    {
        Console.WriteLine("\u001b[1;36m????????????????????????????????????????????\u001b[0m");
        Console.WriteLine("\u001b[1;36m?   NetNIX Library Include Demo            ?\u001b[0m");
        Console.WriteLine("\u001b[1;36m????????????????????????????????????????????\u001b[0m");
        DemoApiLib.Separator();

        // ?? 1. Formatted Output ????????????????????????????????????
        DemoApiLib.PrintHeader("1. Formatted Output (DemoApiLib)");
        DemoApiLib.PrintValue("Username", api.Username);
        DemoApiLib.PrintValue("UID", api.Uid);
        DemoApiLib.PrintOk("Library loaded successfully!");
        DemoApiLib.PrintInfo("This output is produced by DemoApiLib helper methods.");
        DemoApiLib.PrintError("This is what an error looks like.");
        DemoApiLib.Separator();

        // ?? 2. System Summary ??????????????????????????????????????
        DemoApiLib.PrintHeader("2. System Summary");
        foreach (var line in DemoApiLib.GetSystemSummary(api))
            Console.WriteLine($"  {line}");
        DemoApiLib.Separator();

        // ?? 3. File Utilities ??????????????????????????????????????
        DemoApiLib.PrintHeader("3. File Utilities");

        // Find all .cs files in /bin
        var csFiles = DemoApiLib.FindByExtension(api, "/bin", ".cs");
        DemoApiLib.PrintValue("C# scripts in /bin", csFiles.Length);

        // Find all .cs files in /lib
        var libFiles = DemoApiLib.FindByExtension(api, "/lib", ".cs");
        DemoApiLib.PrintValue("Libraries in /lib", libFiles.Length);
        foreach (var lib in libFiles)
        {
            string name = api.NodeName(lib);
            int lines = DemoApiLib.CountLines(api, lib);
            DemoApiLib.PrintInfo($"  {name}: {lines} lines");
        }

        // Extension detection
        DemoApiLib.PrintValue("IsCsFile(\"test.cs\")", DemoApiLib.IsCsFile("test.cs"));
        DemoApiLib.PrintValue("IsShFile(\"demo.sh\")", DemoApiLib.IsShFile("demo.sh"));
        DemoApiLib.PrintValue("GetExtension(\"/bin/ls.cs\")", DemoApiLib.GetExtension("/bin/ls.cs"));
        DemoApiLib.Separator();

        // ?? 4. Directory Size ??????????????????????????????????????
        DemoApiLib.PrintHeader("4. Directory Size Calculator");
        long binSize = DemoApiLib.DirectorySize(api, "/bin");
        long libSize = DemoApiLib.DirectorySize(api, "/lib");
        DemoApiLib.PrintValue("/bin total size", $"{binSize} bytes");
        DemoApiLib.PrintValue("/lib total size", $"{libSize} bytes");
        DemoApiLib.Separator();

        // ?? 5. Table Formatting ????????????????????????????????????
        DemoApiLib.PrintHeader("5. Table Formatting");
        Console.WriteLine("  " + DemoApiLib.TableRow(
            ("NAME", 15), ("TYPE", 6), ("SIZE", 10)));
        Console.WriteLine("  " + DemoApiLib.TableRow(
            ("?????????????", 15), ("??????", 6), ("??????????", 10)));

        // Show first 5 files from /bin
        int shown = 0;
        foreach (var path in csFiles.Take(5))
        {
            string name = api.NodeName(path);
            string ext = DemoApiLib.GetExtension(path);
            int size = api.GetSizeAbsolute(path);
            Console.WriteLine("  " + DemoApiLib.TableRow(
                (name, 15), (ext, 6), (size + "B", 10)));
            shown++;
        }
        if (csFiles.Length > shown)
            Console.WriteLine($"  ... and {csFiles.Length - shown} more");
        DemoApiLib.Separator();

        // ?? Done ???????????????????????????????????????????????????
        DemoApiLib.PrintHeader("Demo Complete");
        DemoApiLib.PrintOk("The #include directive lets scripts share code.");
        DemoApiLib.PrintInfo("Create your own libraries in /lib/");
        DemoApiLib.PrintInfo("Include them with: #include <yourlib>");
        DemoApiLib.Separator();

        return 0;
    }
}
