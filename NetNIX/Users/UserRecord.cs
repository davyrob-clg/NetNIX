/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
using System.Security.Cryptography;
using System.Text;

namespace NetNIX.Users;

public sealed class UserRecord
{
    public int Uid { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // SHA-256 hex
    public int Gid { get; set; }
    public string HomeDirectory { get; set; } = string.Empty;
    public string Shell { get; set; } = "/bin/nsh";

    public string ToPasswdLine() =>
        $"{Username}:x:{Uid}:{Gid}::{HomeDirectory}:{Shell}";

    public static UserRecord? FromPasswdLine(string line)
    {
        var p = line.Split(':');
        if (p.Length < 7) return null;
        return new UserRecord
        {
            Username = p[0],
            Uid = int.Parse(p[2]),
            Gid = int.Parse(p[3]),
            HomeDirectory = p[5],
            Shell = p[6]
        };
    }

    public static string HashPassword(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool VerifyPassword(string plain) =>
        PasswordHash == HashPassword(plain);
}
