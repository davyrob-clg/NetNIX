/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
using System.IO.Compression;
using System.Text;
using NetNIX.Users;
using NetNIX.VFS;

namespace NetNIX.Scripting;

/// <summary>
/// Public API surface exposed to user scripts running inside the NetNIX environment.
/// Scripts receive an instance of this class as their first parameter.
/// </summary>
public sealed class NixApi
{
    private readonly VirtualFileSystem _fs;
    private readonly UserManager _userMgr;

    public int Uid { get; }
    public int Gid { get; }
    public string Username { get; }
    public string Cwd { get; }

    /// <summary>
    /// Cancellation token for daemon scripts. Check this to support graceful shutdown.
    /// For non-daemon scripts this is CancellationToken.None.
    /// </summary>
    public CancellationToken DaemonToken { get; set; } = CancellationToken.None;

    public NixApi(VirtualFileSystem fs, UserManager userMgr, int uid, int gid, string username, string cwd)
    {
        _fs = fs;
        _userMgr = userMgr;
        Uid = uid;
        Gid = gid;
        Username = username;
        Cwd = cwd;
    }

    // ── Path helpers ───────────────────────────────────────────────

    public string ResolvePath(string path) =>
        VirtualFileSystem.ResolvePath(Cwd, path);

    public string GetName(string path) =>
        VirtualFileSystem.GetName(path);

    public string GetParent(string path) =>
        VirtualFileSystem.GetParent(path);

    // ── File system queries ────────────────────────────────────────

    public bool Exists(string path) => _fs.Exists(ResolvePath(path));
    public bool IsDirectory(string path) => _fs.IsDirectory(ResolvePath(path));
    public bool IsFile(string path) => _fs.IsFile(ResolvePath(path));

    public string[] ListDirectory(string path)
    {
        string resolved = ResolvePath(path);
        if (!RequireRead(resolved)) return [];
        return _fs.ListDirectory(resolved).OrderBy(n => n.Name).Select(n => n.Path).ToArray();
    }

    public string GetPermissions(string path)
    {
        var node = _fs.GetNode(ResolvePath(path));
        return node?.Permissions ?? "";
    }

    public string GetPermissionString(string path)
    {
        var node = _fs.GetNode(ResolvePath(path));
        return node?.PermissionString() ?? "";
    }

    public bool IsDir(string vfsPath)
    {
        var node = _fs.GetNode(vfsPath);
        return node?.IsDirectory ?? false;
    }

    public string NodeName(string vfsPath) =>
        VirtualFileSystem.GetName(vfsPath);

    public int GetOwner(string path)
    {
        var node = _fs.GetNode(ResolvePath(path));
        return node?.OwnerId ?? -1;
    }

    public int GetGroup(string path)
    {
        var node = _fs.GetNode(ResolvePath(path));
        return node?.GroupId ?? -1;
    }

    public int GetSize(string path)
    {
        var node = _fs.GetNode(ResolvePath(path));
        if (node == null || node.IsDirectory) return -1;
        return node.Data?.Length ?? 0;
    }

    public bool CanRead(string path)
    {
        var node = _fs.GetNode(ResolvePath(path));
        return node?.CanRead(Uid, Gid) ?? false;
    }

    public bool CanWrite(string path)
    {
        var node = _fs.GetNode(ResolvePath(path));
        return node?.CanWrite(Uid, Gid) ?? false;
    }

    public bool CanExecute(string path)
    {
        var node = _fs.GetNode(ResolvePath(path));
        return node?.CanExecute(Uid, Gid) ?? false;
    }

    // ── Permission enforcement ─────────────────────────────────────

    /// <summary>
    /// Checks that the current user can traverse every directory component
    /// leading up to (but not including) the final segment of <paramref name="resolvedPath"/>.
    /// Root (uid 0) always passes. Returns false and prints a message on denial.
    /// </summary>
    private bool RequireTraverse(string resolvedPath)
    {
        if (Uid == 0) return true;
        var parts = resolvedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";
        // Check every ancestor directory (skip the last segment which is the target itself)
        for (int i = 0; i < parts.Length - 1; i++)
        {
            current += "/" + parts[i];
            var node = _fs.GetNode(current);
            if (node != null && !node.CanExecute(Uid, Gid))
            {
                Console.WriteLine($"Permission denied: {resolvedPath}");
                return false;
            }
        }
        return true;
    }

    private bool RequireRead(string resolvedPath)
    {
        if (!RequireTraverse(resolvedPath)) return false;
        var node = _fs.GetNode(resolvedPath);
        if (node != null && !node.CanRead(Uid, Gid))
        {
            Console.WriteLine($"Permission denied: {resolvedPath}");
            return false;
        }
        return true;
    }

    private bool RequireWrite(string resolvedPath)
    {
        if (!RequireTraverse(resolvedPath)) return false;
        var node = _fs.GetNode(resolvedPath);
        if (node != null && !node.CanWrite(Uid, Gid))
        {
            Console.WriteLine($"Permission denied: {resolvedPath}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// For creating or deleting entries, the parent directory must be writable.
    /// Returns false and prints a message on denial.
    /// </summary>
    private bool RequireParentWrite(string resolvedPath)
    {
        if (!RequireTraverse(resolvedPath)) return false;
        string parent = VirtualFileSystem.GetParent(resolvedPath);
        var parentNode = _fs.GetNode(parent);
        if (parentNode != null && !parentNode.CanWrite(Uid, Gid))
        {
            Console.WriteLine($"Permission denied: {resolvedPath}");
            return false;
        }
        return true;
    }

    // ── File I/O ───────────────────────────────────────────────────

    public string ReadText(string path)
    {
        string resolved = ResolvePath(path);
        if (!RequireRead(resolved)) return "";
        return Encoding.UTF8.GetString(_fs.ReadFile(resolved));
    }

    public byte[] ReadBytes(string path)
    {
        string resolved = ResolvePath(path);
        if (!RequireRead(resolved)) return [];
        return _fs.ReadFile(resolved);
    }

    public void WriteText(string path, string content)
    {
        string resolved = ResolvePath(path);
        byte[] data = Encoding.UTF8.GetBytes(content);
        if (_fs.IsFile(resolved))
        {
            if (!RequireWrite(resolved)) return;
            _fs.WriteFile(resolved, data);
        }
        else
        {
            if (!RequireParentWrite(resolved)) return;
            _fs.CreateFile(resolved, Uid, Gid, data);
        }
    }

    public void WriteBytes(string path, byte[] data)
    {
        string resolved = ResolvePath(path);
        if (_fs.IsFile(resolved))
        {
            if (!RequireWrite(resolved)) return;
            _fs.WriteFile(resolved, data);
        }
        else
        {
            if (!RequireParentWrite(resolved)) return;
            _fs.CreateFile(resolved, Uid, Gid, data);
        }
    }

    public void AppendText(string path, string content)
    {
        string resolved = ResolvePath(path);
        if (_fs.IsFile(resolved))
        {
            if (!RequireWrite(resolved)) return;
            byte[] existing = _fs.ReadFile(resolved);
            byte[] extra = Encoding.UTF8.GetBytes(content);
            byte[] combined = new byte[existing.Length + extra.Length];
            existing.CopyTo(combined, 0);
            extra.CopyTo(combined, existing.Length);
            _fs.WriteFile(resolved, combined);
        }
        else
        {
            if (!RequireParentWrite(resolved)) return;
            _fs.CreateFile(resolved, Uid, Gid, Encoding.UTF8.GetBytes(content));
        }
    }

    public void CreateEmptyFile(string path)
    {
        string resolved = ResolvePath(path);
        if (!_fs.Exists(resolved))
        {
            if (!RequireParentWrite(resolved)) return;
            _fs.CreateFile(resolved, Uid, Gid);
        }
    }

    public void CreateDir(string path)
    {
        string resolved = ResolvePath(path);
        if (!RequireParentWrite(resolved)) return;
        _fs.CreateDirectory(resolved, Uid, Gid);
    }

    public void CreateDirWithParents(string path)
    {
        string resolved = ResolvePath(path);
        var parts = resolved.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string current = "";
        foreach (var part in parts)
        {
            current += "/" + part;
            if (!_fs.Exists(current))
            {
                if (!RequireParentWrite(current)) return;
                _fs.CreateDirectory(current, Uid, Gid);
            }
        }
    }

    public void Delete(string path)
    {
        string resolved = ResolvePath(path);
        if (!RequireParentWrite(resolved)) return;
        // Also require write on the target itself so users can't delete other users' files
        var node = _fs.GetNode(resolved);
        if (node != null && Uid != 0 && node.OwnerId != Uid && !node.CanWrite(Uid, Gid))
        {
            Console.WriteLine($"Permission denied: {resolved}");
            return;
        }
        _fs.Delete(resolved);
    }

    public void Copy(string src, string dest)
    {
        string resolvedSrc = ResolvePath(src);
        string resolvedDest = ResolvePath(dest);
        if (!RequireRead(resolvedSrc)) return;
        if (_fs.IsDirectory(resolvedDest))
            resolvedDest = resolvedDest.TrimEnd('/') + "/" + VirtualFileSystem.GetName(resolvedSrc);
        if (!RequireParentWrite(resolvedDest)) return;
        _fs.Copy(resolvedSrc, resolvedDest, Uid, Gid);
    }

    public void Move(string src, string dest)
    {
        string resolvedSrc = ResolvePath(src);
        string resolvedDest = ResolvePath(dest);
        if (!RequireParentWrite(resolvedSrc)) return;
        // Also require write on the source node itself so users can't move other users' files
        var srcNode = _fs.GetNode(resolvedSrc);
        if (srcNode != null && Uid != 0 && srcNode.OwnerId != Uid && !srcNode.CanWrite(Uid, Gid))
        {
            Console.WriteLine($"Permission denied: {resolvedSrc}");
            return;
        }
        if (_fs.IsDirectory(resolvedDest))
            resolvedDest = resolvedDest.TrimEnd('/') + "/" + VirtualFileSystem.GetName(resolvedSrc);
        if (!RequireParentWrite(resolvedDest)) return;
        _fs.Move(resolvedSrc, resolvedDest);
    }

    /// <summary>Returns all paths in the entire VFS (for find/du).</summary>
    public string[] GetAllPaths()
    {
        return _fs.GetAllPaths();
    }

    /// <summary>Checks if a given absolute VFS path exists.</summary>
    public bool ExistsAbsolute(string vfsPath) => _fs.Exists(vfsPath);

    /// <summary>Checks if a given absolute VFS path is a directory.</summary>
    public bool IsDirAbsolute(string vfsPath) => _fs.IsDirectory(vfsPath);

    /// <summary>Gets the size of a file by absolute VFS path.</summary>
    public int GetSizeAbsolute(string vfsPath)
    {
        var node = _fs.GetNode(vfsPath);
        if (node == null || node.IsDirectory) return -1;
        return node.Data?.Length ?? 0;
    }

    // ?? User queries ???????????????????????????????????????????????

    public string? GetUsername(int uid) =>
        _userMgr.GetUser(uid)?.Username;

    public string? GetGroupName(int gid) =>
        _userMgr.GetGroup(gid)?.Name;

    public int UserCount => _userMgr.Users.Count;
    public int GroupCount => _userMgr.Groups.Count;

    public (int uid, string username, int gid, string home)[] GetAllUsers()
    {
        return _userMgr.Users
            .Select(u => (u.Uid, u.Username, u.Gid, u.HomeDirectory))
            .ToArray();
    }

    public (int gid, string name, string[] members)[] GetAllGroups()
    {
        return _userMgr.Groups
            .Select(g => (g.Gid, g.Name, g.Members.ToArray()))
            .ToArray();
    }

    // ?? User management (root only) ????????????????????????????????

    /// <summary>Prints a permission error if caller is not root. Returns true if root.</summary>
    private bool RequireRoot(string cmd)
    {
        if (Uid != 0)
        {
            Console.Error.WriteLine($"{cmd}: permission denied (must be root)");
            return false;
        }
        return true;
    }

    public void CreateUser(string username, string password, string? home = null)
    {
        if (!RequireRoot("useradd")) return;
        _userMgr.CreateUser(username, password, home);
    }

    public void DeleteUser(string username, bool removeHome = false)
    {
        if (!RequireRoot("userdel")) return;
        if (removeHome) _userMgr.DeleteUserHomeDirectory(username);
        _userMgr.DeleteUser(username);
    }

    public void ChangePassword(string username, string newPassword)
    {
        if (Uid != 0 && username != Username)
        {
            Console.Error.WriteLine("passwd: permission denied");
            return;
        }
        _userMgr.ChangePassword(username, newPassword);
    }

    public void ModifyUser(string username, string? newHome = null, string? newShell = null)
    {
        if (!RequireRoot("usermod")) return;
        _userMgr.ModifyUser(username, newHome, newShell);
    }

    public void LockUser(string username)
    {
        if (!RequireRoot("usermod")) return;
        _userMgr.LockUser(username);
    }

    public void UnlockUser(string username)
    {
        if (!RequireRoot("usermod")) return;
        _userMgr.UnlockUser(username);
    }

    public bool IsUserLocked(string username) => _userMgr.IsLocked(username);

    public void CreateGroup(string name)
    {
        if (!RequireRoot("groupadd")) return;
        _userMgr.CreateGroup(name);
    }

    public void DeleteGroup(string name)
    {
        if (!RequireRoot("groupdel")) return;
        _userMgr.DeleteGroup(name);
    }

    public void RenameGroup(string oldName, string newName)
    {
        if (!RequireRoot("groupmod")) return;
        _userMgr.RenameGroup(oldName, newName);
    }

    public void AddUserToGroup(string username, string groupName)
    {
        if (!RequireRoot("usermod")) return;
        _userMgr.AddUserToGroup(username, groupName);
    }

    public void RemoveUserFromGroup(string username, string groupName)
    {
        if (!RequireRoot("usermod")) return;
        _userMgr.RemoveUserFromGroup(username, groupName);
    }

    public string[] GetUserGroups(string username)
    {
        return _userMgr.Groups
            .Where(g => g.Members.Contains(username))
            .Select(g => g.Name)
            .ToArray();
    }

    // ?? Mount / Unmount ????????????????????????????????????????????

    /// <summary>
    /// Mount a zip archive from the host filesystem into the VFS.
    /// Mounted content is not saved to the rootfs.
    /// If <paramref name="autoSave"/> is true, any mutation under the mount
    /// point is automatically written back to the host zip.
    /// Root only.
    /// </summary>
    public int MountZip(string hostPath, string mountPoint, bool autoSave = false)
    {
        if (!RequireRoot("mount")) return -1;
        string resolvedMount = ResolvePath(mountPoint);
        return _fs.MountZip(hostPath, resolvedMount, Uid, Gid, autoSave);
    }

    /// <summary>
    /// Unmount a previously mounted archive. Root only.
    /// If <paramref name="saveChanges"/> is true, writes modified content
    /// back to the original host zip before unmounting.
    /// </summary>
    public void Unmount(string mountPoint, bool saveChanges = false)
    {
        if (!RequireRoot("umount")) return;
        string resolvedMount = ResolvePath(mountPoint);
        _fs.Unmount(resolvedMount, saveChanges);
    }

    /// <summary>
    /// Save all current content under a mount point back to the original
    /// host zip archive. Root only.
    /// </summary>
    public void SaveMount(string mountPoint)
    {
        if (!RequireRoot("mount")) return;
        string resolvedMount = ResolvePath(mountPoint);
        _fs.SaveMount(resolvedMount);
    }

    /// <summary>
    /// Returns all active mount points, their host zip paths, and auto-save status.
    /// </summary>
    public (string mountPoint, string hostPath, bool autoSave)[] GetMountPoints() => _fs.GetMountPoints();

    // ?? Import ??????????????????????????????????????????????????????

    /// <summary>
    /// Import a file from the host filesystem into the VFS.
    /// Root only. Returns true on success.
    /// </summary>
    public bool ImportFile(string hostPath, string vfsPath)
    {
        if (!RequireRoot("importfile")) return false;
        string resolved = ResolvePath(vfsPath);
        return _fs.ImportFromHost(hostPath, resolved, Uid, Gid);
    }

    /// <summary>
    /// Import a directory (and all its contents recursively) from the host
    /// filesystem into the VFS. Root only. Returns the number of files
    /// imported, or -1 on failure.
    /// </summary>
    public int ImportDirectory(string hostDir, string vfsDir)
    {
        if (!RequireRoot("importfile")) return -1;

        string resolvedVfs = ResolvePath(vfsDir);

        if (!System.IO.Directory.Exists(hostDir))
        {
            Console.Error.WriteLine($"importfile: {hostDir}: host directory not found");
            return -1;
        }

        return ImportDirectoryRecursive(hostDir, resolvedVfs);
    }

    private int ImportDirectoryRecursive(string hostDir, string vfsDir)
    {
        // Ensure the VFS directory exists
        if (!_fs.IsDirectory(vfsDir))
            _fs.CreateDirectory(vfsDir, Uid, Gid, "rwxr-xr-x");

        int count = 0;

        // Import all files in this directory
        foreach (var file in System.IO.Directory.GetFiles(hostDir))
        {
            string fileName = System.IO.Path.GetFileName(file);
            string vfsPath = vfsDir.TrimEnd('/') + "/" + fileName;
            if (_fs.ImportFromHost(file, vfsPath, Uid, Gid))
                count++;
        }

        // Recurse into subdirectories
        foreach (var subDir in System.IO.Directory.GetDirectories(hostDir))
        {
            string dirName = System.IO.Path.GetFileName(subDir);
            string vfsSubDir = vfsDir.TrimEnd('/') + "/" + dirName;
            int sub = ImportDirectoryRecursive(subDir, vfsSubDir);
            if (sub >= 0)
                count += sub;
        }

        return count;
    }

    // ?? Export ??????????????????????????????????????????????????????

    /// <summary>
    /// Export the VFS (or a subtree) to a zip archive on the host filesystem.
    /// Mounted filesystems are excluded unless <paramref name="includeMounts"/> is true.
    /// Root only.
    /// </summary>
    public int ExportFs(string hostPath, string vfsRoot = "/", bool includeMounts = false)
    {
        if (!RequireRoot("export")) return -1;
        string resolved = ResolvePath(vfsRoot);
        return _fs.ExportToZip(hostPath, resolved, includeMounts);
    }

    // ?? Factory reinstall ????????????????????????????????????????????

    /// <summary>
    /// Reinstall factory binaries, libraries, man pages, and factory files.
    /// Overwrites existing files with the versions shipped with the executable.
    /// Root only.
    /// </summary>
    public bool ReinstallFactory()
    {
        if (!RequireRoot("reinstall")) return false;
        NetNIX.Setup.FirstRunSetup.InstallBuiltinScripts(_fs);
        NetNIX.Setup.FirstRunSetup.InstallBuiltinLibs(_fs);
        NetNIX.Setup.FirstRunSetup.InstallManPages(_fs);
        NetNIX.Setup.FirstRunSetup.InstallFactoryFiles(_fs);
        _fs.Save();
        return true;
    }

    // ?? Save ???????????????????????????????????????????????????????

    public void Save() => _fs.Save();

    // ?? Networking ?????????????????????????????????????????????????

    private NixNet? _net;

    /// <summary>
    /// Networking API for HTTP requests from scripts.
    /// </summary>
    public NixNet Net => _net ??= new NixNet();

    /// <summary>
    /// Download a URL and save it to a file in the VFS.
    /// Returns true on success.
    /// </summary>
    public bool Download(string url, string path)
    {
        byte[]? data = Net.GetBytes(url);
        if (data == null) return false;
        WriteBytes(path, data);
        return true;
    }

    /// <summary>
    /// Download a URL as text and save it to a file in the VFS.
    /// Returns true on success.
    /// </summary>
    public bool DownloadText(string url, string path)
    {
        string? text = Net.Get(url);
        if (text == null) return false;
        WriteText(path, text);
        return true;
    }

    // ?? Zip archives ???????????????????????????????????????????????

    /// <summary>
    /// Create a zip archive from files and/or directories in the VFS.
    /// Each source path is added to the zip preserving relative structure.
    /// </summary>
    public bool ZipCreate(string zipPath, params string[] sourcePaths)
    {
        try
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var src in sourcePaths)
                {
                    string resolved = ResolvePath(src);
                    if (_fs.IsFile(resolved))
                    {
                        string name = VirtualFileSystem.GetName(resolved);
                        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                        using var es = entry.Open();
                        byte[] data = _fs.ReadFile(resolved);
                        es.Write(data, 0, data.Length);
                    }
                    else if (_fs.IsDirectory(resolved))
                    {
                        string baseName = VirtualFileSystem.GetName(resolved);
                        AddDirectoryToZip(archive, resolved, baseName);
                    }
                }
            }

            WriteBytes(zipPath, ms.ToArray());
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"zip: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Extract a zip archive from the VFS into a target directory.
    /// Creates the directory if it does not exist.
    /// Returns the number of entries extracted, or -1 on error.
    /// </summary>
    public int ZipExtract(string zipPath, string destDir)
    {
        try
        {
            string resolvedZip = ResolvePath(zipPath);
            byte[] zipData = _fs.ReadFile(resolvedZip);
            string resolvedDest = ResolvePath(destDir);

            if (!_fs.Exists(resolvedDest))
                CreateDirWithParents(destDir);

            using var ms = new MemoryStream(zipData);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

            int count = 0;
            foreach (var entry in archive.Entries)
            {
                string entryPath = resolvedDest.TrimEnd('/') + "/" + entry.FullName.Replace('\\', '/');

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    if (!_fs.Exists(entryPath))
                    {
                        var parts = entryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        string current = "";
                        foreach (var part in parts)
                        {
                            current += "/" + part;
                            if (!_fs.Exists(current))
                                _fs.CreateDirectory(current, Uid, Gid);
                        }
                    }
                }
                else
                {
                    string parentDir = VirtualFileSystem.GetParent(entryPath);
                    if (!_fs.Exists(parentDir))
                    {
                        var parts = parentDir.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        string current = "";
                        foreach (var part in parts)
                        {
                            current += "/" + part;
                            if (!_fs.Exists(current))
                                _fs.CreateDirectory(current, Uid, Gid);
                        }
                    }

                    using var es = entry.Open();
                    using var buf = new MemoryStream();
                    es.CopyTo(buf);
                    byte[] data = buf.ToArray();

                    if (_fs.IsFile(entryPath))
                        _fs.WriteFile(entryPath, data);
                    else
                        _fs.CreateFile(entryPath, Uid, Gid, data);

                    count++;
                }
            }
            return count;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"unzip: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// List the contents of a zip archive stored in the VFS.
    /// Returns an array of (name, compressedSize, uncompressedSize) tuples.
    /// Returns null on error.
    /// </summary>
    public (string name, long compressed, long uncompressed)[]? ZipList(string zipPath)
    {
        try
        {
            string resolved = ResolvePath(zipPath);
            byte[] zipData = _fs.ReadFile(resolved);

            using var ms = new MemoryStream(zipData);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

            var entries = new List<(string, long, long)>();
            foreach (var entry in archive.Entries)
                entries.Add((entry.FullName, entry.CompressedLength, entry.Length));

            return entries.ToArray();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"zip: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Add a single file from the VFS into an existing zip archive (or create new).
    /// The entryName controls the path inside the zip.
    /// </summary>
    public bool ZipAddFile(string zipPath, string filePath, string? entryName = null)
    {
        try
        {
            string resolvedZip = ResolvePath(zipPath);
            string resolvedFile = ResolvePath(filePath);

            byte[]? existingZip = _fs.IsFile(resolvedZip) ? _fs.ReadFile(resolvedZip) : null;
            var ms = new MemoryStream();

            if (existingZip != null)
            {
                ms.Write(existingZip, 0, existingZip.Length);
                ms.Seek(0, SeekOrigin.Begin);
            }

            using (var archive = new ZipArchive(ms, existingZip != null ? ZipArchiveMode.Update : ZipArchiveMode.Create, leaveOpen: true))
            {
                string name = entryName ?? VirtualFileSystem.GetName(resolvedFile);
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var es = entry.Open();
                byte[] data = _fs.ReadFile(resolvedFile);
                es.Write(data, 0, data.Length);
            }

            WriteBytes(zipPath, ms.ToArray());
            ms.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"zip: {ex.Message}");
            return false;
        }
    }

    private void AddDirectoryToZip(ZipArchive archive, string vfsDir, string zipPrefix)
    {
        foreach (var node in _fs.ListDirectory(vfsDir))
        {
            string entryPath = zipPrefix + "/" + node.Name;

            if (node.IsDirectory)
            {
                AddDirectoryToZip(archive, node.Path, entryPath);
            }
            else
            {
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                using var es = entry.Open();
                byte[] data = _fs.ReadFile(node.Path);
                es.Write(data, 0, data.Length);
            }
        }
    }

    // ?? Host clipboard ?????????????????????????????????????????????

    /// <summary>
    /// Reads the host system's clipboard text content.
    /// Returns null if the clipboard is empty or unavailable.
    /// </summary>
    public string? GetClipboard()
    {
        try
        {
            string? text = null;

            if (OperatingSystem.IsWindows())
            {
                text = RunProcess("powershell.exe", "-NoProfile -Command Get-Clipboard");
            }
            else if (OperatingSystem.IsMacOS())
            {
                text = RunProcess("pbpaste", "");
            }
            else if (OperatingSystem.IsLinux())
            {
                // Try xclip first, fall back to xsel
                text = RunProcess("xclip", "-selection clipboard -o")
                    ?? RunProcess("xsel", "--clipboard --output");
            }

            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the host clipboard and writes it to a file in the VFS.
    /// Returns true if successful, false if clipboard was empty or unavailable.
    /// </summary>
    public bool ClipboardToFile(string path)
    {
        string? text = GetClipboard();
        if (text == null) return false;
        WriteText(path, text);
        return true;
    }

    /// <summary>
    /// Reads the host clipboard and appends it to a file in the VFS.
    /// Returns true if successful, false if clipboard was empty or unavailable.
    /// </summary>
    public bool ClipboardAppendToFile(string path)
    {
        string? text = GetClipboard();
        if (text == null) return false;
        AppendText(path, text);
        return true;
    }

    /// <summary>
    /// Sets the host system's clipboard to the given text.
    /// Returns true on success, false on failure.
    /// </summary>
    public bool SetClipboard(string text)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return RunProcessWithInput("clip.exe", "", text);
            }
            else if (OperatingSystem.IsMacOS())
            {
                return RunProcessWithInput("pbcopy", "", text);
            }
            else if (OperatingSystem.IsLinux())
            {
                return RunProcessWithInput("xclip", "-selection clipboard", text)
                    || RunProcessWithInput("xsel", "--clipboard --input", text);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a file from the VFS and copies its content to the host clipboard.
    /// Returns true on success.
    /// </summary>
    public bool FileToClipboard(string path)
    {
        string resolved = ResolvePath(path);
        if (!_fs.IsFile(resolved)) return false;
        string text = Encoding.UTF8.GetString(_fs.ReadFile(resolved));
        return SetClipboard(text);
    }

    private static string? RunProcess(string fileName, string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return null;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            return proc.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool RunProcessWithInput(string fileName, string arguments, string input)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return false;

            proc.StandardInput.Write(input);
            proc.StandardInput.Close();
            proc.WaitForExit(3000);

            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
