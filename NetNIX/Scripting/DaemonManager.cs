/*
Copyright (C) 2026 Michael Sullender
This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
You should have received a copy of the GNU General Public License along with this program. If not, see gnu.org
*/
using System.Text;
using NetNIX.Users;
using NetNIX.VFS;

namespace NetNIX.Scripting;

/// <summary>
/// Manages background daemon processes within the NetNIX environment.
/// Daemons are .cs scripts that implement:
///     static int Daemon(NixApi api, string[] args)
/// They run on background threads and can be started/stopped by root.
/// </summary>
public sealed class DaemonManager
{
    private readonly VirtualFileSystem _fs;
    private readonly UserManager _userMgr;
    private readonly ScriptRunner _scriptRunner;
    private readonly Dictionary<int, DaemonInfo> _daemons = [];
    private int _nextPid = 1000;

    public DaemonManager(VirtualFileSystem fs, UserManager userMgr, ScriptRunner scriptRunner)
    {
        _fs = fs;
        _userMgr = userMgr;
        _scriptRunner = scriptRunner;
    }

    /// <summary>
    /// Start a daemon from a VFS script path. Root only.
    /// Returns the PID, or -1 on failure.
    /// </summary>
    public int Start(string name, string vfsPath, string[] args, UserRecord user, string cwd)
    {
        if (user.Uid != 0)
        {
            Console.Error.WriteLine("daemon: permission denied (must be root)");
            return -1;
        }

        // Check if already running
        foreach (var d in _daemons.Values)
        {
            if (d.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && d.IsRunning)
            {
                Console.Error.WriteLine($"daemon: '{name}' is already running (pid {d.Pid})");
                return -1;
            }
        }

        if (!_fs.IsFile(vfsPath))
        {
            Console.Error.WriteLine($"daemon: {vfsPath}: no such file");
            return -1;
        }

        // Compile the daemon script (with sandbox exceptions applied)
        string source = Encoding.UTF8.GetString(_fs.ReadFile(vfsPath));
        var assembly = _scriptRunner.CompileWithExceptions(source, name, vfsPath);
        if (assembly == null) return -1;

        // Find the Daemon(NixApi, string[]) method
        System.Reflection.MethodInfo? entry = null;
        foreach (var type in assembly.GetTypes())
        {
            entry = type.GetMethod("Daemon",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                [typeof(NixApi), typeof(string[])]);
            if (entry != null) break;
        }

        if (entry == null)
        {
            Console.Error.WriteLine($"daemon: {name}: script has no static Daemon(NixApi, string[]) method");
            return -1;
        }

        int pid = _nextPid++;
        var api = new NixApi(_fs, _userMgr, user.Uid, user.Gid, user.Username, cwd);

        var info = new DaemonInfo
        {
            Pid = pid,
            Name = name,
            ScriptPath = vfsPath,
            Owner = user.Username,
            StartedAt = DateTime.Now,
            Cts = new CancellationTokenSource(),
        };

        var cts = info.Cts;
        var daemonArgs = args.Append("--daemon-token").Append(cts.Token.GetHashCode().ToString()).ToArray();

        info.Thread = new Thread(() =>
        {
            try
            {
                // Store the CancellationToken in NixApi so daemons can check it
                api.DaemonToken = cts.Token;
                entry.Invoke(null, [api, args]);
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                Console.Error.WriteLine($"daemon[{name}]: {ex.InnerException.Message}");
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"daemon[{name}]: {ex.Message}");
            }
            finally
            {
                info.StoppedAt = DateTime.Now;
            }
        })
        {
            Name = $"daemon-{name}-{pid}",
            IsBackground = true,
        };

        _daemons[pid] = info;
        info.Thread.Start();

        return pid;
    }

    /// <summary>
    /// Stop a running daemon by name or PID. Root only.
    /// </summary>
    public bool Stop(string nameOrPid, int callerUid)
    {
        if (callerUid != 0)
        {
            Console.Error.WriteLine("daemon: permission denied (must be root)");
            return false;
        }

        var info = FindDaemon(nameOrPid);
        if (info == null)
        {
            Console.Error.WriteLine($"daemon: '{nameOrPid}' not found");
            return false;
        }

        if (!info.IsRunning)
        {
            Console.Error.WriteLine($"daemon: '{info.Name}' is not running");
            return false;
        }

        info.Cts.Cancel();

        // Give the thread a moment to exit gracefully
        if (!info.Thread!.Join(TimeSpan.FromSeconds(3)))
        {
            Console.Error.WriteLine($"daemon: '{info.Name}' did not stop in time (still running in background)");
        }

        return true;
    }

    /// <summary>
    /// Get status info for all known daemons.
    /// </summary>
    public DaemonInfo[] List()
    {
        return _daemons.Values.OrderBy(d => d.Pid).ToArray();
    }

    /// <summary>
    /// Get status for a single daemon by name or PID.
    /// </summary>
    public DaemonInfo? GetStatus(string nameOrPid)
    {
        return FindDaemon(nameOrPid);
    }

    private DaemonInfo? FindDaemon(string nameOrPid)
    {
        if (int.TryParse(nameOrPid, out int pid) && _daemons.TryGetValue(pid, out var byPid))
            return byPid;

        // Find by name (most recent)
        return _daemons.Values
            .Where(d => d.Name.Equals(nameOrPid, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.Pid)
            .FirstOrDefault();
    }
}

public sealed class DaemonInfo
{
    public int Pid { get; init; }
    public string Name { get; init; } = "";
    public string ScriptPath { get; init; } = "";
    public string Owner { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public DateTime? StoppedAt { get; set; }
    public CancellationTokenSource Cts { get; init; } = new();
    public Thread? Thread { get; set; }

    public bool IsRunning => Thread?.IsAlive == true;
    public string Status => IsRunning ? "running" : "stopped";
}
