/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
using System.IO.Compression;
using System.Text;

namespace NetNIX.VFS;

/// <summary>
/// A virtual file system backed by a .zip archive on the host OS.
/// All paths inside the VFS are UNIX-style (forward-slash, rooted at "/").
/// </summary>
public sealed class VirtualFileSystem
{
    private readonly string _archivePath;
    private readonly Dictionary<string, VfsNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _mountPoints = new(StringComparer.Ordinal);
    private readonly HashSet<string> _autoSaveMounts = new(StringComparer.Ordinal);

    public VirtualFileSystem(string archivePath)
    {
        _archivePath = archivePath;
    }

    // ?? Persistence ????????????????????????????????????????????????

    public void Load()
    {
        _nodes.Clear();
        // Always ensure root directory exists
        _nodes["/"] = new VfsNode("/", isDirectory: true, ownerId: 0, groupId: 0, permissions: "rwxr-xr-x");

        if (!File.Exists(_archivePath))
            return;

        var fi = new FileInfo(_archivePath);
        if (fi.Length == 0)
            return;

        using var stream = File.OpenRead(_archivePath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in zip.Entries)
        {
            // Metadata is stored in a special entry
            if (entry.FullName == ".vfsmeta")
                continue;

            string vfsPath = "/" + entry.FullName.Replace('\\', '/').TrimEnd('/');
            bool isDir = entry.FullName.EndsWith('/');

            byte[]? data = null;
            if (!isDir)
            {
                using var es = entry.Open();
                using var ms = new MemoryStream();
                es.CopyTo(ms);
                data = ms.ToArray();
            }

            _nodes[vfsPath] = new VfsNode(vfsPath, isDir, 0, 0, isDir ? "rwxr-xr-x" : "rw-r--r--")
            {
                Data = data
            };
        }

        // Load metadata overlay (owner, group, permissions)
        var metaEntry = zip.GetEntry(".vfsmeta");
        if (metaEntry != null)
        {
            using var ms = metaEntry.Open();
            using var reader = new StreamReader(ms);
            while (reader.ReadLine() is { } line)
            {
                // Format: path\towner\tgroup\tperms
                var trimmed = line.TrimEnd('\r');
                var parts = trimmed.Split('\t');
                if (parts.Length < 4) continue;
                string path = parts[0];
                if (_nodes.TryGetValue(path, out var node))
                {
                    node.OwnerId = int.Parse(parts[1]);
                    node.GroupId = int.Parse(parts[2]);
                    node.Permissions = parts[3];
                }
            }
        }
    }

    public void Save()
    {
        string tempPath = _archivePath + ".tmp";
        using (var stream = File.Create(tempPath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var metaSb = new StringBuilder();

            foreach (var (path, node) in _nodes.OrderBy(kv => kv.Key))
            {
                if (path == "/") continue; // root is implicit

                // Skip nodes that belong to a mounted archive (transient)
                if (IsMounted(path)) continue;

                string entryName = path.TrimStart('/');

                if (node.IsDirectory)
                {
                    zip.CreateEntry(entryName + "/");
                }
                else
                {
                    var entry = zip.CreateEntry(entryName, CompressionLevel.SmallestSize);
                    if (node.Data != null)
                    {
                        using var es = entry.Open();
                        es.Write(node.Data, 0, node.Data.Length);
                    }
                }

                metaSb.AppendLine($"{path}\t{node.OwnerId}\t{node.GroupId}\t{node.Permissions}");
            }

            // Write metadata
            var meta = zip.CreateEntry(".vfsmeta");
            using (var ms = meta.Open())
            using (var writer = new StreamWriter(ms))
            {
                writer.Write(metaSb.ToString());
            }
        }
        File.Move(tempPath, _archivePath, overwrite: true);
    }

    // ?? Query ??????????????????????????????????????????????????????

    public bool Exists(string path) => _nodes.ContainsKey(NormalizePath(path));
    public bool IsDirectory(string path) => _nodes.TryGetValue(NormalizePath(path), out var n) && n.IsDirectory;
    public bool IsFile(string path) => _nodes.TryGetValue(NormalizePath(path), out var n) && !n.IsDirectory;

    public VfsNode? GetNode(string path)
    {
        _nodes.TryGetValue(NormalizePath(path), out var node);
        return node;
    }

    public IEnumerable<VfsNode> ListDirectory(string path)
    {
        path = NormalizePath(path);
        if (!IsDirectory(path))
            return [];

        string prefix = path == "/" ? "/" : path + "/";
        var results = new List<VfsNode>();
        foreach (var (k, v) in _nodes)
        {
            if (k == path) continue;
            if (!k.StartsWith(prefix)) continue;
            // Only immediate children
            string remainder = k[prefix.Length..];
            if (!remainder.Contains('/'))
                results.Add(v);
        }
        return results;
    }

    public string[] GetAllPaths() =>
        _nodes.Keys.OrderBy(k => k).ToArray();

    // ?? Mutation ???????????????????????????????????????????????????

    /// <summary>
    /// If the given path falls under an auto-save mount, save that mount.
    /// </summary>
    private void AutoSaveIfMounted(string normalizedPath)
    {
        foreach (var mp in _autoSaveMounts)
        {
            if (normalizedPath == mp || normalizedPath.StartsWith(mp + "/"))
            {
                SaveMount(mp);
                return;
            }
        }
    }

    public VfsNode CreateDirectory(string path, int ownerId, int groupId, string permissions = "rwxr-xr-x")
    {
        path = NormalizePath(path);
        if (_nodes.ContainsKey(path))
            throw new IOException($"Path already exists: {path}");

        if (!EnsureParentExists(path))
            return null;

        var node = new VfsNode(path, true, ownerId, groupId, permissions);
        _nodes[path] = node;
        AutoSaveIfMounted(path);
        return node;
    }

    public VfsNode CreateFile(string path, int ownerId, int groupId, byte[]? data = null, string permissions = "rw-r--r--")
    {
        path = NormalizePath(path);
        if (!EnsureParentExists(path))
            return null;

        var node = new VfsNode(path, false, ownerId, groupId, permissions)
        {
            Data = data
        };
        _nodes[path] = node;
        AutoSaveIfMounted(path);
        return node;
    }

    public void WriteFile(string path, byte[] data)
    {
        path = NormalizePath(path);
        if (!_nodes.TryGetValue(path, out var node) || node.IsDirectory)
            throw new IOException($"Not a file: {path}");
        node.Data = data;
        AutoSaveIfMounted(path);
    }

    public byte[] ReadFile(string path)
    {
        path = NormalizePath(path);
        if (!_nodes.TryGetValue(path, out var node) || node.IsDirectory)
            throw new IOException($"Not a file: {path}");
        return node.Data ?? [];
    }

    public void Delete(string path)
    {
        path = NormalizePath(path);
        if (path == "/") throw new IOException("Cannot delete root");
        if (!_nodes.ContainsKey(path))
            throw new IOException($"Path not found: {path}");

        // If directory, remove everything beneath it
        var toRemove = _nodes.Keys.Where(k => k == path || k.StartsWith(path + "/")).ToList();
        foreach (var k in toRemove)
            _nodes.Remove(k);
        AutoSaveIfMounted(path);
    }

    public void Move(string src, string dest)
    {
        src = NormalizePath(src);
        dest = NormalizePath(dest);

        if (!_nodes.ContainsKey(src))
            throw new IOException($"Source not found: {src}");

        if (!EnsureParentExists(dest))
            return;

        var keysToMove = _nodes.Keys.Where(k => k == src || k.StartsWith(src + "/")).ToList();
        var movedPairs = new List<(string oldKey, VfsNode node)>();

        foreach (var key in keysToMove)
        {
            var node = _nodes[key];
            _nodes.Remove(key);
            string newKey = dest + key[src.Length..];
            node.Path = newKey;
            movedPairs.Add((key, node));
        }

        foreach (var (_, node) in movedPairs)
            _nodes[node.Path] = node;
        AutoSaveIfMounted(src);
        AutoSaveIfMounted(dest);
    }

    public void Copy(string src, string dest, int ownerId, int groupId)
    {
        src = NormalizePath(src);
        dest = NormalizePath(dest);

        if (!_nodes.TryGetValue(src, out var srcNode))
            throw new IOException($"Source not found: {src}");

        if (!EnsureParentExists(dest))
            return;

        if (srcNode.IsDirectory)
        {
            var keysToMove = _nodes.Keys.Where(k => k == src || k.StartsWith(src + "/")).ToList();
            foreach (var key in keysToMove)
            {
                var orig = _nodes[key];
                string newKey = dest + key[src.Length..];
                _nodes[newKey] = new VfsNode(newKey, orig.IsDirectory, ownerId, groupId, orig.Permissions)
                {
                    Data = orig.Data?.ToArray()
                };
            }
        }
        else
        {
            _nodes[dest] = new VfsNode(dest, false, ownerId, groupId, srcNode.Permissions)
            {
                Data = srcNode.Data?.ToArray()
            };
        }
        AutoSaveIfMounted(dest);
    }

    // ?? Mount / Unmount ????????????????????????????????????????????

    /// <summary>
    /// Mount a zip archive from the host filesystem into the VFS at the
    /// given mount point. The mount point directory is created if needed.
    /// Mounted content is not saved to the rootfs archive.
    /// If <paramref name="autoSave"/> is true, mutations under this mount
    /// point are automatically written back to the host zip.
    /// </summary>
    public int MountZip(string hostPath, string mountPoint, int ownerId, int groupId, bool autoSave = false)
    {
        mountPoint = NormalizePath(mountPoint);

        if (!File.Exists(hostPath))
        {
            Console.Error.WriteLine($"mount: {hostPath}: no such file on host");
            return -1;
        }

        // Create mount point directory if needed
        if (!_nodes.ContainsKey(mountPoint))
        {
            if (!EnsureParentExists(mountPoint))
                return -1;
            _nodes[mountPoint] = new VfsNode(mountPoint, true, ownerId, groupId, "rwxr-xr-x");
        }
        else if (!_nodes[mountPoint].IsDirectory)
        {
            throw new IOException($"Mount point is not a directory: {mountPoint}");
        }

        _mountPoints[mountPoint] = hostPath;
        if (autoSave)
            _autoSaveMounts.Add(mountPoint);
        else
            _autoSaveMounts.Remove(mountPoint);

        using var stream = File.OpenRead(hostPath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        int count = 0;
        foreach (var entry in zip.Entries)
        {
            string entryName = entry.FullName.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(entryName)) continue;

            string vfsPath = mountPoint.TrimEnd('/') + "/" + entryName;
            bool isDir = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');

            if (isDir)
            {
                // Ensure the full directory chain exists
                var parts = vfsPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                string current = "";
                foreach (var part in parts)
                {
                    current += "/" + part;
                    if (!_nodes.ContainsKey(current))
                        _nodes[current] = new VfsNode(current, true, ownerId, groupId, "rwxr-xr-x");
                }
            }
            else
            {
                // Ensure parent directories exist
                string parent = GetParent(vfsPath);
                var parentParts = parent.Split('/', StringSplitOptions.RemoveEmptyEntries);
                string cur = "";
                foreach (var part in parentParts)
                {
                    cur += "/" + part;
                    if (!_nodes.ContainsKey(cur))
                        _nodes[cur] = new VfsNode(cur, true, ownerId, groupId, "rwxr-xr-x");
                }

                using var es = entry.Open();
                using var ms = new MemoryStream();
                es.CopyTo(ms);

                _nodes[vfsPath] = new VfsNode(vfsPath, false, ownerId, groupId, "rw-r--r--")
                {
                    Data = ms.ToArray()
                };
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Unmount a previously mounted archive, removing all nodes under the mount point.
    /// If <paramref name="saveChanges"/> is true, writes modified content back to the
    /// original host zip before unmounting.
    /// </summary>
    public void Unmount(string mountPoint, bool saveChanges = false)
    {
        mountPoint = NormalizePath(mountPoint);
        if (!_mountPoints.TryGetValue(mountPoint, out var hostPath))
            throw new IOException($"Not a mount point: {mountPoint}");

        if (saveChanges)
            SaveMount(mountPoint);

        _mountPoints.Remove(mountPoint);
        _autoSaveMounts.Remove(mountPoint);

        string prefix = mountPoint + "/";
        var toRemove = _nodes.Keys
            .Where(k => k == mountPoint || k.StartsWith(prefix))
            .ToList();
        foreach (var k in toRemove)
            _nodes.Remove(k);
    }

    /// <summary>
    /// Write all current content under a mount point back to the original
    /// host zip archive, replacing its contents entirely.
    /// </summary>
    public void SaveMount(string mountPoint)
    {
        mountPoint = NormalizePath(mountPoint);
        if (!_mountPoints.TryGetValue(mountPoint, out var hostPath))
            throw new IOException($"Not a mount point: {mountPoint}");

        using var stream = File.Create(hostPath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        string prefix = mountPoint.TrimEnd('/') + "/";

        foreach (var (path, node) in _nodes.OrderBy(kv => kv.Key))
        {
            if (path == mountPoint) continue; // skip the mount root itself
            if (!path.StartsWith(prefix)) continue;

            // Entry name is relative to the mount point
            string entryName = path[prefix.Length..];

            if (node.IsDirectory)
            {
                zip.CreateEntry(entryName + "/");
            }
            else
            {
                var entry = zip.CreateEntry(entryName, CompressionLevel.SmallestSize);
                if (node.Data != null)
                {
                    using var es = entry.Open();
                    es.Write(node.Data, 0, node.Data.Length);
                }
            }
        }
    }

    /// <summary>
    /// Returns true if the given path is at or below a mount point.
    /// </summary>
    public bool IsMounted(string path)
    {
        path = NormalizePath(path);
        foreach (var mp in _mountPoints.Keys)
        {
            if (path == mp || path.StartsWith(mp + "/"))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns all active mount points, their host zip paths, and whether auto-save is enabled.
    /// </summary>
    public (string mountPoint, string hostPath, bool autoSave)[] GetMountPoints() =>
        _mountPoints.OrderBy(kv => kv.Key)
            .Select(kv => (kv.Key, kv.Value, _autoSaveMounts.Contains(kv.Key)))
            .ToArray();

    // ?? Import ?????????????????????????????????????????????????????

    /// <summary>
    /// Import a file from the host filesystem into the VFS.
    /// Returns true on success, false on failure (error printed to console).
    /// </summary>
    public bool ImportFromHost(string hostPath, string vfsPath, int ownerId, int groupId)
    {
        vfsPath = NormalizePath(vfsPath);

        byte[] data;
        try
        {
            data = File.ReadAllBytes(hostPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"importfile: {hostPath}: {ex.Message}");
            return false;
        }

        if (!EnsureParentExists(vfsPath))
            return false;
        var node = new VfsNode(vfsPath, false, ownerId, groupId, "rw-r--r--")
        {
            Data = data
        };
        _nodes[vfsPath] = node;
        AutoSaveIfMounted(vfsPath);
        return true;
    }

    // ?? Export ?????????????????????????????????????????????????????

    /// <summary>
    /// Export VFS contents under <paramref name="vfsRoot"/> to a zip archive
    /// on the host filesystem at <paramref name="hostPath"/>.
    /// Mounted filesystems are excluded unless <paramref name="includeMounts"/> is true.
    /// Returns the number of files exported.
    /// </summary>
    public int ExportToZip(string hostPath, string vfsRoot = "/", bool includeMounts = false)
    {
        vfsRoot = NormalizePath(vfsRoot);
        string prefix = vfsRoot == "/" ? "/" : vfsRoot + "/";

        FileStream stream;
        try
        {
            stream = File.Create(hostPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"export: {hostPath}: {ex.Message}");
            return -1;
        }

        using (stream)
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            int count = 0;
            foreach (var (path, node) in _nodes.OrderBy(kv => kv.Key))
            {
                // Skip root itself
                if (path == "/") continue;

                // Skip mounted content unless opted in
                if (!includeMounts && IsMounted(path)) continue;

                // Must be at or under the export root
                string entryName;
                if (vfsRoot == "/")
                {
                    entryName = path.TrimStart('/');
                }
                else if (path == vfsRoot)
                {
                    continue; // skip the root directory entry itself
                }
                else if (path.StartsWith(prefix))
                {
                    entryName = path[prefix.Length..];
                }
                else
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entryName)) continue;

                if (node.IsDirectory)
                {
                    zip.CreateEntry(entryName + "/");
                }
                else
                {
                    var entry = zip.CreateEntry(entryName, CompressionLevel.SmallestSize);
                    if (node.Data != null)
                    {
                        using var es = entry.Open();
                        es.Write(node.Data, 0, node.Data.Length);
                    }
                    count++;
                }
            }

            return count;
        }
    }

    // ?? Helpers ????????????????????????????????????????????????????

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";

        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new Stack<string>();

        foreach (var part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count > 0) stack.Pop();
                continue;
            }
            stack.Push(part);
        }

        return "/" + string.Join("/", stack.Reverse());
    }

    public static string ResolvePath(string cwd, string input)
    {
        if (input.StartsWith('/'))
            return NormalizePath(input);
        return NormalizePath(cwd.TrimEnd('/') + "/" + input);
    }

    public static string GetParent(string path)
    {
        path = NormalizePath(path);
        if (path == "/") return "/";
        int idx = path.LastIndexOf('/');
        return idx <= 0 ? "/" : path[..idx];
    }

    public static string GetName(string path)
    {
        path = NormalizePath(path);
        int idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    private bool EnsureParentExists(string path)
    {
        string parent = GetParent(path);
        if (!_nodes.ContainsKey(parent))
        {
            Console.Error.WriteLine($"vfs: parent directory does not exist: {parent}");
            return false;
        }
        if (!_nodes[parent].IsDirectory)
        {
            Console.Error.WriteLine($"vfs: parent is not a directory: {parent}");
            return false;
        }
        return true;
    }
}
