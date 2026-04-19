/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
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
