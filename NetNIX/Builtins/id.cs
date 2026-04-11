using System;
using System.Linq;
using NetNIX.Scripting;

public static class IdCommand
{
    public static int Run(NixApi api, string[] args)
    {
        int uid = api.Uid;
        string username = api.Username;

        if (args.Length > 0)
        {
            var users = api.GetAllUsers();
            var match = users.FirstOrDefault(u => u.username == args[0]);
            if (match.username == null)
            {
                Console.WriteLine($"id: '{args[0]}': no such user");
                return 1;
            }
            uid = match.uid;
            username = match.username;
        }

        int gid = uid == api.Uid ? api.Gid : api.GetAllUsers().First(u => u.uid == uid).gid;
        string groupName = api.GetGroupName(gid) ?? gid.ToString();
        Console.WriteLine($"uid={uid}({username}) gid={gid}({groupName})");
        return 0;
    }
}
