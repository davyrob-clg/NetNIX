/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
namespace NetNIX.VFS;
 //v2
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
