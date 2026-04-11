using System;
using System.Linq;
using NetNIX.Scripting;

public static class DemoCommand
{
    private const string DemoDir = "/tmp/demo";
    private const string Bar = "????????????????????????????????????????";

    public static int Run(NixApi api, string[] args)
    {
        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            PrintUsage();
            return 0;
        }

        string section = args.Length > 0 ? args[0].ToLower() : "all";

        Console.WriteLine("\u001b[1;36m????????????????????????????????????????????\u001b[0m");
        Console.WriteLine("\u001b[1;36m?     NetNIX API Demo & Walkthrough        ?\u001b[0m");
        Console.WriteLine("\u001b[1;36m????????????????????????????????????????????\u001b[0m");
        Console.WriteLine();

        // Clean up from any previous run
        if (api.Exists(DemoDir))
            api.Delete(DemoDir);

        bool all = section == "all";

        if (all || section == "props")      DemoProperties(api);
        if (all || section == "paths")      DemoPaths(api);
        if (all || section == "dirs")       DemoDirectories(api);
        if (all || section == "files")      DemoFiles(api);
        if (all || section == "readwrite")  DemoReadWrite(api);
        if (all || section == "copy")       DemoCopyMove(api);
        if (all || section == "perms")      DemoPermissions(api);
        if (all || section == "list")       DemoListing(api);
        if (all || section == "search")     DemoSearch(api);
        if (all || section == "users")      DemoUsers(api);
        if (all || section == "save")       DemoSave(api);

        // Clean up
        if (api.Exists(DemoDir))
            api.Delete(DemoDir);
        api.Save();

        Console.WriteLine();
        Header("Demo Complete");
        Console.WriteLine("  All temporary files have been cleaned up.");
        Console.WriteLine("  Run 'man api' for the full API reference.");
        Console.WriteLine("  Run 'man scripting' for scripting guide.");
        Console.WriteLine();

        return 0;
    }

    // ?? Section: Properties ????????????????????????????????????????

    private static void DemoProperties(NixApi api)
    {
        Header("1. Properties — api.Uid, api.Gid, api.Username, api.Cwd");

        Show("api.Uid",      api.Uid);
        Show("api.Gid",      api.Gid);
        Show("api.Username", api.Username);
        Show("api.Cwd",      api.Cwd);
        Console.WriteLine();
    }

    // ?? Section: Path helpers ??????????????????????????????????????

    private static void DemoPaths(NixApi api)
    {
        Header("2. Path Helpers — ResolvePath, GetName, GetParent");

        Show("api.ResolvePath(\"..\")",           api.ResolvePath(".."));
        Show("api.ResolvePath(\"file.txt\")",     api.ResolvePath("file.txt"));
        Show("api.ResolvePath(\"/bin/ls.cs\")",   api.ResolvePath("/bin/ls.cs"));
        Show("api.GetName(\"/bin/ls.cs\")",       api.GetName("/bin/ls.cs"));
        Show("api.GetParent(\"/bin/ls.cs\")",     api.GetParent("/bin/ls.cs"));
        Console.WriteLine();
    }

    // ?? Section: Directories ???????????????????????????????????????

    private static void DemoDirectories(NixApi api)
    {
        Header("3. Directories — CreateDir, CreateDirWithParents, IsDirectory");

        Console.WriteLine("  Creating /tmp/demo ...");
        api.CreateDir(DemoDir);
        Show("api.Exists(DemoDir)",       api.Exists(DemoDir));
        Show("api.IsDirectory(DemoDir)",  api.IsDirectory(DemoDir));
        Show("api.IsFile(DemoDir)",       api.IsFile(DemoDir));

        Console.WriteLine("  Creating nested dirs with CreateDirWithParents ...");
        api.CreateDirWithParents(DemoDir + "/a/b/c");
        Show("api.Exists(DemoDir + \"/a/b/c\")", api.Exists(DemoDir + "/a/b/c"));
        Console.WriteLine();
    }

    // ?? Section: Files ?????????????????????????????????????????????

    private static void DemoFiles(NixApi api)
    {
        Header("4. Files — CreateEmptyFile, WriteText, IsFile");

        string path = DemoDir + "/hello.txt";
        Console.WriteLine("  Creating empty file ...");
        api.CreateEmptyFile(path);
        Show("api.Exists(path)",  api.Exists(path));
        Show("api.IsFile(path)",  api.IsFile(path));
        Show("api.GetSize(path)", api.GetSize(path));

        Console.WriteLine("  Writing text to file ...");
        api.WriteText(path, "Hello, NetNIX!\nThis is line 2.\nLine 3.\n");
        Show("api.GetSize(path) after write", api.GetSize(path));
        Console.WriteLine();
    }

    // ?? Section: Read / Write ??????????????????????????????????????

    private static void DemoReadWrite(NixApi api)
    {
        Header("5. Read/Write — ReadText, ReadBytes, WriteText, WriteBytes, AppendText");

        string path = DemoDir + "/hello.txt";
        if (!api.IsFile(path))
            api.WriteText(path, "Hello, NetNIX!\nThis is line 2.\nLine 3.\n");

        Console.WriteLine("  ReadText:");
        string text = api.ReadText(path);
        foreach (var line in text.Split('\n'))
            Console.WriteLine("    | " + line);

        Console.WriteLine("  ReadBytes:");
        byte[] bytes = api.ReadBytes(path);
        Show("  byte count", bytes.Length);

        Console.WriteLine("  AppendText ...");
        api.AppendText(path, "Appended line!\n");
        text = api.ReadText(path);
        int lineCount = text.Split('\n').Length;
        Show("  line count after append", lineCount);

        Console.WriteLine("  WriteBytes ...");
        string binPath = DemoDir + "/data.bin";
        byte[] rawData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        api.WriteBytes(binPath, rawData);
        byte[] readBack = api.ReadBytes(binPath);
        Show("  bytes match", readBack.SequenceEqual(rawData));
        Console.WriteLine();
    }

    // ?? Section: Copy / Move ???????????????????????????????????????

    private static void DemoCopyMove(NixApi api)
    {
        Header("6. Copy & Move — Copy, Move, Delete");

        string src = DemoDir + "/hello.txt";
        if (!api.IsFile(src))
            api.WriteText(src, "Hello, NetNIX!\n");

        string copyDest = DemoDir + "/hello_copy.txt";
        Console.WriteLine("  Copy ...");
        api.Copy(src, copyDest);
        Show("api.Exists(copyDest)", api.Exists(copyDest));
        Show("contents match", api.ReadText(src) == api.ReadText(copyDest));

        string moveDest = DemoDir + "/hello_moved.txt";
        Console.WriteLine("  Move ...");
        api.Move(copyDest, moveDest);
        Show("api.Exists(copyDest) after move", api.Exists(copyDest));
        Show("api.Exists(moveDest) after move", api.Exists(moveDest));

        Console.WriteLine("  Delete ...");
        api.Delete(moveDest);
        Show("api.Exists(moveDest) after delete", api.Exists(moveDest));
        Console.WriteLine();
    }

    // ?? Section: Permissions ???????????????????????????????????????

    private static void DemoPermissions(NixApi api)
    {
        Header("7. Permissions — GetPermissions, GetPermissionString, CanRead/Write/Execute");

        string path = DemoDir + "/hello.txt";
        if (!api.IsFile(path))
            api.WriteText(path, "test\n");

        Show("api.GetPermissions(path)",       api.GetPermissions(path));
        Show("api.GetPermissionString(path)",  api.GetPermissionString(path));
        Show("api.GetOwner(path)",             api.GetOwner(path));
        Show("api.GetGroup(path)",             api.GetGroup(path));
        Show("api.CanRead(path)",              api.CanRead(path));
        Show("api.CanWrite(path)",             api.CanWrite(path));
        Show("api.CanExecute(path)",           api.CanExecute(path));
        Console.WriteLine();
    }

    // ?? Section: Directory Listing ?????????????????????????????????

    private static void DemoListing(NixApi api)
    {
        Header("8. Directory Listing — ListDirectory, NodeName, IsDir, GetSize");

        // Ensure some files exist for listing
        api.WriteText(DemoDir + "/file1.txt", "aaa\n");
        api.WriteText(DemoDir + "/file2.txt", "bbbbb\n");

        Console.WriteLine($"  Listing {DemoDir}:");
        var entries = api.ListDirectory(DemoDir);
        Show("  entry count", entries.Length);

        foreach (var entry in entries)
        {
            string name = api.NodeName(entry);
            bool isDir = api.IsDir(entry);
            string type = isDir ? "DIR " : "FILE";
            int size = isDir ? -1 : api.GetSizeAbsolute(entry);
            string sizeStr = size >= 0 ? $"{size}B" : "-";
            Console.WriteLine($"    [{type}] {name,-20} {sizeStr,8}");
        }
        Console.WriteLine();
    }

    // ?? Section: Search ????????????????????????????????????????????

    private static void DemoSearch(NixApi api)
    {
        Header("9. Search — GetAllPaths, ExistsAbsolute, IsDirAbsolute, GetSizeAbsolute");

        var allPaths = api.GetAllPaths();
        Show("  total VFS paths", allPaths.Length);

        Console.WriteLine("  Finding all .txt files under " + DemoDir + ":");
        int found = 0;
        foreach (var path in allPaths)
        {
            if (!path.StartsWith(DemoDir)) continue;
            if (api.IsDirAbsolute(path)) continue;
            if (!path.EndsWith(".txt")) continue;

            string name = api.NodeName(path);
            int size = api.GetSizeAbsolute(path);
            Console.WriteLine($"    {path,-40} {size}B");
            found++;
        }
        Show("  .txt files found", found);

        Console.WriteLine("  ExistsAbsolute / IsDirAbsolute:");
        Show("  api.ExistsAbsolute(\"/bin\")",    api.ExistsAbsolute("/bin"));
        Show("  api.IsDirAbsolute(\"/bin\")",     api.IsDirAbsolute("/bin"));
        Show("  api.ExistsAbsolute(\"/nope\")",   api.ExistsAbsolute("/nope"));
        Console.WriteLine();
    }

    // ?? Section: Users ?????????????????????????????????????????????

    private static void DemoUsers(NixApi api)
    {
        Header("10. Users & Groups — GetUsername, GetGroupName, GetAllUsers, GetAllGroups");

        Show("api.UserCount",  api.UserCount);
        Show("api.GroupCount", api.GroupCount);

        Console.WriteLine("  GetUsername / GetGroupName:");
        Show("  api.GetUsername(0)",  api.GetUsername(0) ?? "(null)");
        Show("  api.GetGroupName(0)", api.GetGroupName(0) ?? "(null)");

        Console.WriteLine("  GetAllUsers:");
        foreach (var (uid, username, gid, home) in api.GetAllUsers())
            Console.WriteLine($"    uid={uid} {username,-12} gid={gid} home={home}");

        Console.WriteLine("  GetAllGroups:");
        foreach (var (gid, name, members) in api.GetAllGroups())
            Console.WriteLine($"    gid={gid} {name,-12} members=[{string.Join(", ", members)}]");

        Console.WriteLine();
    }

    // ?? Section: Save ??????????????????????????????????????????????

    private static void DemoSave(NixApi api)
    {
        Header("11. Persistence — api.Save()");

        Console.WriteLine("  api.Save() writes the entire VFS to disk.");
        Console.WriteLine("  All changes are persisted to the rootfs.zip archive.");
        Console.WriteLine("  Call it after making changes you want to keep.");
        Console.WriteLine();
    }

    // ?? Helpers ????????????????????????????????????????????????????

    private static void Header(string title)
    {
        Console.WriteLine($"\u001b[1;33m  {Bar}\u001b[0m");
        Console.WriteLine($"\u001b[1;33m  {title}\u001b[0m");
        Console.WriteLine($"\u001b[1;33m  {Bar}\u001b[0m");
    }

    private static void Show(string label, object value)
    {
        Console.WriteLine($"  \u001b[36m{label}\u001b[0m = \u001b[1;37m{value}\u001b[0m");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("demo — demonstrate the entire NixApi");
        Console.WriteLine();
        Console.WriteLine("Usage: demo [section]");
        Console.WriteLine();
        Console.WriteLine("Sections:");
        Console.WriteLine("  all        Run all demos (default)");
        Console.WriteLine("  props      Properties (Uid, Gid, Username, Cwd)");
        Console.WriteLine("  paths      Path helpers (ResolvePath, GetName, GetParent)");
        Console.WriteLine("  dirs       Directory operations (CreateDir, CreateDirWithParents)");
        Console.WriteLine("  files      File creation (CreateEmptyFile, WriteText)");
        Console.WriteLine("  readwrite  Read/Write (ReadText, ReadBytes, AppendText, WriteBytes)");
        Console.WriteLine("  copy       Copy & Move (Copy, Move, Delete)");
        Console.WriteLine("  perms      Permissions (GetPermissions, CanRead/Write/Execute)");
        Console.WriteLine("  list       Directory listing (ListDirectory, NodeName, IsDir)");
        Console.WriteLine("  search     Search (GetAllPaths, ExistsAbsolute, IsDirAbsolute)");
        Console.WriteLine("  users      Users & Groups (GetAllUsers, GetAllGroups)");
        Console.WriteLine("  save       Persistence (Save)");
    }
}
