namespace NetNIX.VFS;

public sealed class VfsNode
{
    public string Path { get; set; }
    public bool IsDirectory { get; }
    public int OwnerId { get; set; }
    public int GroupId { get; set; }
    public string Permissions { get; set; } // e.g. "rwxr-xr-x"
    public byte[]? Data { get; set; }

    public string Name => VirtualFileSystem.GetName(Path);

    public VfsNode(string path, bool isDirectory, int ownerId, int groupId, string permissions)
    {
        Path = path;
        IsDirectory = isDirectory;
        OwnerId = ownerId;
        GroupId = groupId;
        Permissions = permissions;
    }

    // ?? Permission helpers ?????????????????????????????????????????

    /// <summary>
    /// Permissions string is 9 chars: rwxrwxrwx  (owner / group / other).
    /// </summary>
    public bool CanRead(int uid, int gid)
    {
        if (uid == 0) return true; // root
        if (OwnerId == uid) return Permissions.Length >= 1 && Permissions[0] == 'r';
        if (GroupId == gid) return Permissions.Length >= 4 && Permissions[3] == 'r';
        return Permissions.Length >= 7 && Permissions[6] == 'r';
    }

    public bool CanWrite(int uid, int gid)
    {
        if (uid == 0) return true;
        if (OwnerId == uid) return Permissions.Length >= 2 && Permissions[1] == 'w';
        if (GroupId == gid) return Permissions.Length >= 5 && Permissions[4] == 'w';
        return Permissions.Length >= 8 && Permissions[7] == 'w';
    }

    public bool CanExecute(int uid, int gid)
    {
        if (uid == 0) return true;
        if (OwnerId == uid) return Permissions.Length >= 3 && Permissions[2] == 'x';
        if (GroupId == gid) return Permissions.Length >= 6 && Permissions[5] == 'x';
        return Permissions.Length >= 9 && Permissions[8] == 'x';
    }

    public string PermissionString()
    {
        string type = IsDirectory ? "d" : "-";
        return type + Permissions;
    }
}
