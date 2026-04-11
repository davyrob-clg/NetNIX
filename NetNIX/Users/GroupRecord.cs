namespace NetNIX.Users;

public sealed class GroupRecord
{
    public int Gid { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Members { get; set; } = [];

    public string ToGroupLine() =>
        $"{Name}:x:{Gid}:{string.Join(',', Members)}";

    public static GroupRecord? FromGroupLine(string line)
    {
        var p = line.Split(':');
        if (p.Length < 4) return null;
        return new GroupRecord
        {
            Name = p[0],
            Gid = int.Parse(p[2]),
            Members = p[3].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
        };
    }
}
