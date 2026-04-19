/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
using System.Text;
using NetNIX.VFS;

namespace NetNIX.Users;

/// <summary>
/// Manages users and groups stored inside the virtual file system
/// in /etc/passwd, /etc/shadow, and /etc/group.
/// </summary>
public sealed class UserManager
{
    private readonly VirtualFileSystem _fs;
    private readonly List<UserRecord> _users = [];
    private readonly List<GroupRecord> _groups = [];

    public IReadOnlyList<UserRecord> Users => _users;
    public IReadOnlyList<GroupRecord> Groups => _groups;

    public UserManager(VirtualFileSystem fs)
    {
        _fs = fs;
    }

    // ?? Load / Save ????????????????????????????????????????????????

    public void Load()
    {
        _users.Clear();
        _groups.Clear();

        if (_fs.IsFile("/etc/passwd"))
        {
            var text = Encoding.UTF8.GetString(_fs.ReadFile("/etc/passwd"));
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var u = UserRecord.FromPasswdLine(line.TrimEnd('\r'));
                if (u != null) _users.Add(u);
            }
        }

        if (_fs.IsFile("/etc/shadow"))
        {
            var text = Encoding.UTF8.GetString(_fs.ReadFile("/etc/shadow"));
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.TrimEnd('\r');
                var parts = trimmed.Split(':');
                if (parts.Length < 2) continue;
                var user = _users.FirstOrDefault(u => u.Username == parts[0]);
                if (user != null)
                    user.PasswordHash = parts[1];
            }
        }

        if (_fs.IsFile("/etc/group"))
        {
            var text = Encoding.UTF8.GetString(_fs.ReadFile("/etc/group"));
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var g = GroupRecord.FromGroupLine(line.TrimEnd('\r'));
                if (g != null) _groups.Add(g);
            }
        }
    }

    public void Save()
    {
        // /etc/passwd
        var passwdSb = new StringBuilder();
        foreach (var u in _users)
            passwdSb.AppendLine(u.ToPasswdLine());
        WriteVfsFile("/etc/passwd", passwdSb.ToString());

        // /etc/shadow (username:hash)
        var shadowSb = new StringBuilder();
        foreach (var u in _users)
            shadowSb.AppendLine($"{u.Username}:{u.PasswordHash}");
        WriteVfsFile("/etc/shadow", shadowSb.ToString());

        // /etc/group
        var groupSb = new StringBuilder();
        foreach (var g in _groups)
            groupSb.AppendLine(g.ToGroupLine());
        WriteVfsFile("/etc/group", groupSb.ToString());
    }

    // ?? User operations ????????????????????????????????????????????

    public UserRecord? GetUser(string username) =>
        _users.FirstOrDefault(u => u.Username == username);

    public UserRecord? GetUser(int uid) =>
        _users.FirstOrDefault(u => u.Uid == uid);

    public GroupRecord? GetGroup(int gid) =>
        _groups.FirstOrDefault(g => g.Gid == gid);

    public GroupRecord? GetGroup(string name) =>
        _groups.FirstOrDefault(g => g.Name == name);

    public UserRecord? Authenticate(string username, string password)
    {
        var user = GetUser(username);
        if (user == null) return null;
        return user.VerifyPassword(password) ? user : null;
    }

    public UserRecord CreateUser(string username, string password, string? homeDir = null)
    {
        if (_users.Any(u => u.Username == username))
            throw new InvalidOperationException($"User '{username}' already exists.");

        int uid = _users.Count == 0 ? 0 : _users.Max(u => u.Uid) + 1;
        int gid = uid; // Each user gets a personal group

        var group = new GroupRecord { Gid = gid, Name = username };
        _groups.Add(group);

        string home = homeDir ?? (uid == 0 ? "/root" : $"/home/{username}");

        var user = new UserRecord
        {
            Uid = uid,
            Username = username,
            PasswordHash = UserRecord.HashPassword(password),
            Gid = gid,
            HomeDirectory = home,
            Shell = "/bin/nsh"
        };
        _users.Add(user);

        // Create home directory if needed
        if (!_fs.Exists(home))
        {
            EnsureDirectoryTree(home, uid, gid);
            CopySkelFiles(home, uid, gid);
        }

        Save();
        return user;
    }

    public void ChangePassword(string username, string newPassword)
    {
        var user = GetUser(username) ?? throw new InvalidOperationException("User not found.");
        user.PasswordHash = UserRecord.HashPassword(newPassword);
        Save();
    }

    public void DeleteUser(string username)
    {
        var user = GetUser(username) ?? throw new InvalidOperationException("User not found.");
        _users.Remove(user);
        var grp = _groups.FirstOrDefault(g => g.Gid == user.Gid && g.Name == username);
        if (grp != null) _groups.Remove(grp);
        Save();
    }

    public void ModifyUser(string username, string? newHome = null, string? newShell = null)
    {
        var user = GetUser(username) ?? throw new InvalidOperationException("User not found.");
        if (newHome != null) user.HomeDirectory = newHome;
        if (newShell != null) user.Shell = newShell;
        Save();
    }

    public void LockUser(string username)
    {
        var user = GetUser(username) ?? throw new InvalidOperationException("User not found.");
        if (!user.PasswordHash.StartsWith("!"))
            user.PasswordHash = "!" + user.PasswordHash;
        Save();
    }

    public void UnlockUser(string username)
    {
        var user = GetUser(username) ?? throw new InvalidOperationException("User not found.");
        if (user.PasswordHash.StartsWith("!"))
            user.PasswordHash = user.PasswordHash[1..];
        Save();
    }

    public bool IsLocked(string username)
    {
        var user = GetUser(username);
        return user?.PasswordHash.StartsWith("!") ?? false;
    }

    // ?? Group operations ????????????????????????????????????????????

    public GroupRecord CreateGroup(string name)
    {
        if (_groups.Any(g => g.Name == name))
            throw new InvalidOperationException($"Group '{name}' already exists.");

        int gid = _groups.Count == 0 ? 0 : _groups.Max(g => g.Gid) + 1;
        var group = new GroupRecord { Gid = gid, Name = name };
        _groups.Add(group);
        Save();
        return group;
    }

    public void DeleteGroup(string name)
    {
        var group = GetGroup(name) ?? throw new InvalidOperationException($"Group '{name}' not found.");
        // Don't allow deleting a group that is a user's primary group
        if (_users.Any(u => u.Gid == group.Gid))
            throw new InvalidOperationException($"Cannot delete group '{name}': is a primary group for a user.");
        _groups.Remove(group);
        Save();
    }

    public void RenameGroup(string oldName, string newName)
    {
        var group = GetGroup(oldName) ?? throw new InvalidOperationException($"Group '{oldName}' not found.");
        if (_groups.Any(g => g.Name == newName))
            throw new InvalidOperationException($"Group '{newName}' already exists.");
        group.Name = newName;
        Save();
    }

    public void AddUserToGroup(string username, string groupName)
    {
        var user = GetUser(username);
        if (user == null)
        {
            Console.WriteLine($"usermod: user '{username}' not found");
            return;
        }
        var group = GetGroup(groupName);
        if (group == null)
        {
            Console.WriteLine($"usermod: group '{groupName}' not found");
            return;
        }
        if (!group.Members.Contains(username))
        {
            group.Members.Add(username);
            Save();
        }
    }

    public void RemoveUserFromGroup(string username, string groupName)
    {
        var group = GetGroup(groupName) ?? throw new InvalidOperationException($"Group '{groupName}' not found.");
        group.Members.Remove(username);
        Save();
    }

    public void DeleteUserHomeDirectory(string username)
    {
        var user = GetUser(username) ?? throw new InvalidOperationException("User not found.");
        if (_fs.Exists(user.HomeDirectory))
            DeleteRecursive(user.HomeDirectory);
    }

    private void DeleteRecursive(string path)
    {
        if (_fs.IsDirectory(path))
        {
            foreach (var child in _fs.ListDirectory(path))
                DeleteRecursive(child.Path);
        }
        _fs.Delete(path);
    }

    // ?? Helpers ????????????????????????????????????????????????????

    private void EnsureDirectoryTree(string path, int ownerId, int groupId)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";
        for (int i = 0; i < parts.Length; i++)
        {
            current += "/" + parts[i];
            if (!_fs.Exists(current))
            {
                // The final segment is the home directory itself — restrict to owner only (rwx------)
                // Intermediate directories (e.g. /home) stay world-readable (rwxr-xr-x)
                bool isFinal = i == parts.Length - 1;
                string perms = isFinal ? "rwx------" : "rwxr-xr-x";
                _fs.CreateDirectory(current, ownerId, groupId, perms);
            }
        }
    }

    /// <summary>
    /// Copies files from /etc/skel into a new user's home directory.
    /// This provides default config files like .nshrc for every new user.
    /// </summary>
    private void CopySkelFiles(string home, int uid, int gid)
    {
        const string skelDir = "/etc/skel";
        if (!_fs.IsDirectory(skelDir)) return;

        foreach (var entry in _fs.ListDirectory(skelDir))
        {
            if (entry.IsDirectory) continue;
            string destPath = home.TrimEnd('/') + "/" + entry.Name;
            if (_fs.Exists(destPath)) continue;

            byte[] data = _fs.ReadFile(entry.Path);
            _fs.CreateFile(destPath, uid, gid, data, "rw-r--r--");
        }
    }

    private void WriteVfsFile(string path, string content)
    {
        var data = Encoding.UTF8.GetBytes(content);
        if (_fs.IsFile(path))
            _fs.WriteFile(path, data);
        else
            _fs.CreateFile(path, 0, 0, data, "rw-r-----");
    }
}
