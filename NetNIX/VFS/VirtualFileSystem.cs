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
        using var stream = File.Create(_archivePath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        var metaSb = new StringBuilder();

        foreach (var (path, node) in _nodes.OrderBy(kv => kv.Key))
        {
            if (path == "/") continue; // root is implicit

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

    public VfsNode CreateDirectory(string path, int ownerId, int groupId, string permissions = "rwxr-xr-x")
    {
        path = NormalizePath(path);
        if (_nodes.ContainsKey(path))
            throw new IOException($"Path already exists: {path}");

        EnsureParentExists(path);

        var node = new VfsNode(path, true, ownerId, groupId, permissions);
        _nodes[path] = node;
        return node;
    }

    public VfsNode CreateFile(string path, int ownerId, int groupId, byte[]? data = null, string permissions = "rw-r--r--")
    {
        path = NormalizePath(path);
        EnsureParentExists(path);

        var node = new VfsNode(path, false, ownerId, groupId, permissions)
        {
            Data = data
        };
        _nodes[path] = node;
        return node;
    }

    public void WriteFile(string path, byte[] data)
    {
        path = NormalizePath(path);
        if (!_nodes.TryGetValue(path, out var node) || node.IsDirectory)
            throw new IOException($"Not a file: {path}");
        node.Data = data;
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
    }

    public void Move(string src, string dest)
    {
        src = NormalizePath(src);
        dest = NormalizePath(dest);

        if (!_nodes.ContainsKey(src))
            throw new IOException($"Source not found: {src}");

        EnsureParentExists(dest);

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
    }

    public void Copy(string src, string dest, int ownerId, int groupId)
    {
        src = NormalizePath(src);
        dest = NormalizePath(dest);

        if (!_nodes.TryGetValue(src, out var srcNode))
            throw new IOException($"Source not found: {src}");

        EnsureParentExists(dest);

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

    private void EnsureParentExists(string path)
    {
        string parent = GetParent(path);
        if (!_nodes.ContainsKey(parent))
            throw new IOException($"Parent directory does not exist: {parent}");
        if (!_nodes[parent].IsDirectory)
            throw new IOException($"Parent is not a directory: {parent}");
    }
}
