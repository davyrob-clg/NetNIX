using System;
using System.Linq;
using NetNIX.Scripting;

public static class LsCommand
{
    public static int Run(NixApi api, string[] args)
    {
        var argList = args.ToList();

        bool longFormat = argList.Remove("-l") | argList.Remove("-la") | argList.Remove("-al");
        bool showAll = argList.Remove("-a") || longFormat;

        string target = argList.Count > 0 ? argList[0] : ".";
        string resolved = api.ResolvePath(target);

        if (!api.IsDirectory(target))
        {
            if (api.Exists(target))
            {
                PrintEntry(api, resolved, longFormat);
                return 0;
            }
            Console.WriteLine($"ls: cannot access '{target}': No such file or directory");
            return 1;
        }

        var entries = api.ListDirectory(target);

        bool any = false;
        foreach (var entryPath in entries)
        {
            string name = api.NodeName(entryPath);
            if (!showAll && name.StartsWith('.')) continue;
            PrintEntry(api, entryPath, longFormat);
            any = true;
        }

        if (!longFormat && any)
            Console.WriteLine();

        return 0;
    }

    private static void PrintEntry(NixApi api, string vfsPath, bool longFormat)
    {
        string name = api.NodeName(vfsPath);
        bool isDir = api.IsDir(vfsPath);

        if (longFormat)
        {
            string perms = api.GetPermissionString(vfsPath);
            int ownerId = api.GetOwner(vfsPath);
            int groupId = api.GetGroup(vfsPath);
            string ownerName = api.GetUsername(ownerId) ?? ownerId.ToString();
            string groupName = api.GetGroupName(groupId) ?? groupId.ToString();
            string size = isDir ? "-" : api.GetSize(vfsPath).ToString();
            Console.WriteLine($"{perms}  {ownerName,8} {groupName,8}  {size,8}  {name}");
        }
        else
        {
            if (isDir)
                Console.Write($"\u001b[34m{name}/\u001b[0m  ");
            else
                Console.Write($"{name}  ");
        }
    }
}
