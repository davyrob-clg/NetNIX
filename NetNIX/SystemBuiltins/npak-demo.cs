using System;
using NetNIX.Scripting;

/// <summary>
/// npak-demo — demonstrates creating, installing, running, and removing
/// a .npak package step by step.
/// </summary>
public static class NpakDemoCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            Console.WriteLine("npak-demo — walk through creating and installing a .npak package");
            Console.WriteLine("Usage: npak-demo");
            return 0;
        }

        if (api.Uid != 0)
        {
            Console.WriteLine("npak-demo: this demo must be run as root (installs a package)");
            return 1;
        }

        string staging = "/tmp/npak-demo";

        // Cleanup staging from any previous run (keep the .npak file)
        if (api.Exists(staging)) api.Delete(staging);

        Console.WriteLine();
        Console.WriteLine("\u001b[1;36m=== npak Package Demo ===\u001b[0m");
        Console.WriteLine();

        // Step 1: Create the package directory structure
        Step("1", "Create the package directory structure");
        Console.WriteLine("  mkdir /tmp/npak-demo");
        Console.WriteLine("  mkdir /tmp/npak-demo/bin");
        Console.WriteLine("  mkdir /tmp/npak-demo/man");
        api.CreateDirWithParents(staging + "/bin");
        api.CreateDirWithParents(staging + "/man");
        Console.WriteLine("  \u001b[32mDone\u001b[0m");
        Console.WriteLine();

        // Step 2: Write the manifest
        Step("2", "Write manifest.txt (package metadata)");
        string manifest =
            "name=hello\n" +
            "version=1.0\n" +
            "description=A simple hello world demo package\n" +
            "type=app\n";
        api.WriteText(staging + "/manifest.txt", manifest);
        Console.WriteLine("  \u001b[33mmanifest.txt:\u001b[0m");
        foreach (var line in manifest.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            Console.WriteLine($"    {line}");
        Console.WriteLine();

        // Step 3: Write the hello.cs script
        Step("3", "Write bin/hello.cs (the application script)");
        string helloScript = @"using System;
using NetNIX.Scripting;

public static class HelloCommand
{
    public static int Run(NixApi api, string[] args)
    {
        string name = args.Length > 0 ? args[0] : ""world"";
        Console.WriteLine($""Hello, {name}! Installed via npak."");
        return 0;
    }
}
";
        api.WriteText(staging + "/bin/hello.cs", helloScript);
        Console.WriteLine("  \u001b[33mbin/hello.cs:\u001b[0m");
        foreach (var line in helloScript.Split('\n'))
            Console.WriteLine($"    {line}");
        Console.WriteLine();

        // Step 4: Write a man page
        Step("4", "Write man/hello.txt (manual page)");
        string manPage =
            "HELLO(1)                  NetNIX Manual                  HELLO(1)\n\n" +
            "NAME\n    hello - say hello\n\n" +
            "SYNOPSIS\n    hello [name]\n\n" +
            "DESCRIPTION\n    Prints a hello message. Optionally greets the given name.\n    Installed via npak package manager.\n\n" +
            "EXAMPLES\n    hello\n    hello Alice\n\n" +
            "SEE ALSO\n    npak\n";
        api.WriteText(staging + "/man/hello.txt", manPage);
        Console.WriteLine("  \u001b[33mman/hello.txt written\u001b[0m");
        Console.WriteLine();

        // Step 5: Build the .npak package (zip with top-level entries)
        Step("5", "Build the .npak package");
        Console.WriteLine("  Packing /tmp/npak-demo into /tmp/hello.npak ...");

        // Add files one at a time with correct entry paths
        api.ZipAddFile("/tmp/hello.npak", staging + "/manifest.txt", "manifest.txt");
        api.ZipAddFile("/tmp/hello.npak", staging + "/bin/hello.cs", "bin/hello.cs");
        api.ZipAddFile("/tmp/hello.npak", staging + "/man/hello.txt", "man/hello.txt");

        int size = api.GetSize("/tmp/hello.npak");
        Console.WriteLine($"  \u001b[32mCreated /tmp/hello.npak ({size} bytes)\u001b[0m");
        Console.WriteLine();

        // Step 6: Install the package
        Step("6", "Install the package with npak");
        Console.WriteLine("  \u001b[33m$ npak install /tmp/hello.npak\u001b[0m");
        Console.WriteLine();

        // Inline install: extract, copy files, write receipt
        string installStaging = "/tmp/.npak-staging";
        if (api.IsDirectory(installStaging)) api.Delete(installStaging);
        api.CreateDirWithParents(installStaging);
        api.ZipExtract("/tmp/hello.npak", installStaging);

        // Copy bin/ files to /usr/local/bin/
        var installedFiles = new System.Collections.Generic.List<string>();
        string binSrc = installStaging + "/bin/hello.cs";
        string binDest = "/usr/local/bin/hello.cs";
        if (api.IsFile(binSrc))
        {
            api.WriteBytes(binDest, api.ReadBytes(binSrc));
            installedFiles.Add(binDest);
            Console.WriteLine($"  {binDest}");
        }

        // Copy man/ files to /usr/share/man/
        string manSrc = installStaging + "/man/hello.txt";
        string manDest = "/usr/share/man/hello.txt";
        if (api.IsFile(manSrc))
        {
            api.WriteBytes(manDest, api.ReadBytes(manSrc));
            installedFiles.Add(manDest);
            Console.WriteLine($"  {manDest}");
        }

        // Write receipt
        string receiptContent = "name=hello\nversion=1.0\ndescription=A simple hello world demo package\ntype=app\n[files]\n";
        foreach (var f in installedFiles)
            receiptContent += f + "\n";
        api.WriteText("/var/lib/npak/hello.list", receiptContent);

        if (api.IsDirectory(installStaging)) api.Delete(installStaging);
        Console.WriteLine($"  {installedFiles.Count} file(s) installed");
        Console.WriteLine("  npak: hello 1.0 installed successfully");
        api.Save();
        Console.WriteLine();

        // Step 7: Run the installed command
        Step("7", "Run the installed 'hello' command");
        Console.WriteLine("  \u001b[33m$ hello\u001b[0m");
        Console.Write("  ");
        HelloRun(api, new string[0]);
        Console.WriteLine("  \u001b[33m$ hello NetNIX\u001b[0m");
        Console.Write("  ");
        HelloRun(api, new[] { "NetNIX" });
        Console.WriteLine();

        // Step 8: Show package info
        Step("8", "Show package info");
        Console.WriteLine("  \u001b[33m$ npak info hello\u001b[0m");
        if (api.IsFile("/var/lib/npak/hello.list"))
        {
            string receipt = api.ReadText("/var/lib/npak/hello.list");
            Console.WriteLine("  Name:        hello");
            Console.WriteLine("  Version:     1.0");
            Console.WriteLine("  Type:        app");
            Console.WriteLine("  Description: A simple hello world demo package");
            Console.WriteLine($"  Files:       {installedFiles.Count}");
            foreach (var f in installedFiles)
                Console.WriteLine($"    {f}");
        }
        Console.WriteLine();

        // Step 9: Show the man page
        Step("9", "View the installed man page");
        Console.WriteLine("  \u001b[33m$ man hello\u001b[0m");
        if (api.IsFile("/usr/share/man/hello.txt"))
        {
            string page = api.ReadText("/usr/share/man/hello.txt");
            foreach (var line in page.Split('\n'))
                Console.WriteLine($"  {line}");
        }
        Console.WriteLine();

        // Step 10: Clean up demo (remove package + staging)
        Step("10", "Clean up — remove the package");
        Console.WriteLine("  \u001b[33m$ npak remove hello\u001b[0m");

        // Inline remove: delete installed files, delete receipt
        int removed = 0;
        foreach (var f in installedFiles)
        {
            if (api.IsFile(f))
            {
                api.Delete(f);
                Console.WriteLine($"  removed {f}");
                removed++;
            }
        }
        if (api.IsFile("/var/lib/npak/hello.list"))
            api.Delete("/var/lib/npak/hello.list");
        Console.WriteLine($"  npak: hello removed ({removed} file(s))");

        if (api.Exists(staging)) api.Delete(staging);
        api.Save();

        Console.WriteLine();
        Console.WriteLine("\u001b[1;32m=== Demo complete! ===\u001b[0m");
        Console.WriteLine();
        Console.WriteLine("  The example package is still available at /tmp/hello.npak");
        Console.WriteLine("  You can inspect it with:  unzip -l /tmp/hello.npak");
        Console.WriteLine("  Or reinstall it with:     npak install /tmp/hello.npak");
        Console.WriteLine();
        Console.WriteLine("  To create your own packages, follow the same steps:");
        Console.WriteLine("    1. Create a directory with manifest.txt, bin/, lib/, man/");
        Console.WriteLine("    2. Zip it as a .npak file");
        Console.WriteLine("    3. npak install <file.npak>");
        Console.WriteLine();
        Console.WriteLine("  See 'man npak' for full documentation.");
        Console.WriteLine();

        return 0;
    }

    /// <summary>
    /// Inline hello execution for the demo (since the script is dynamically installed).
    /// </summary>
    private static void HelloRun(NixApi api, string[] args)
    {
        string name = args.Length > 0 ? args[0] : "world";
        Console.WriteLine($"Hello, {name}! Installed via npak.");
    }

    private static void Step(string num, string title)
    {
        Console.WriteLine($"\u001b[1;33mStep {num}:\u001b[0m {title}");
    }
}
