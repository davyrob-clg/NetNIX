using System;
using System.Linq;
using NetNIX.Scripting;

//v3
public static class MountCommand
{
    public static int Run(NixApi api, string[] args)
    {
        if (args.Length == 0)
        {
            // List active mount points
            var mounts = api.GetMountPoints();
            if (mounts.Length == 0)
            {
                Console.WriteLine("No active mounts.");
            }
            else
            {
                foreach (var (mp, hp, auto) in mounts)
                {
                    string mode = auto ? "rw" : "ro";
                    Console.WriteLine($"{hp}  on  {mp}  ({mode})");
                }
            }
            return 0;
        }

        if (args.Any(a => a == "-h" || a == "--help"))
        {
            PrintUsage();
            return 0;
        }

        // mount --sync <mount-point>  — save changes to host zip
        if (args[0] == "--sync" || args[0] == "-s")
        {
            if (api.Uid != 0)
            {
                Console.WriteLine("mount: permission denied (must be root)");
                return 1;
            }
            if (args.Length < 2)
            {
                Console.WriteLine("mount: --sync requires a mount point");
                return 1;
            }
            try
            {
                string mp = args[1];
                api.SaveMount(mp);
                Console.WriteLine($"mount: saved changes to {api.ResolvePath(mp)}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"mount: {ex.Message}");
                return 1;
            }
        }

        var argList = args.ToList();
        bool autoSave = argList.Remove("-w") | argList.Remove("--rw");

        if (argList.Count < 2)
        {
            PrintUsage();
            return 1;
        }

        string hostPath = argList[0];
        string mountPoint = argList[1];

        if (api.Uid != 0)
        {
            Console.WriteLine("mount: permission denied (must be root)");
            return 1;
        }

        try
        {
            int count = api.MountZip(hostPath, mountPoint, autoSave);
            string mode = autoSave ? "rw" : "ro";
            Console.WriteLine($"mount: mounted {hostPath} at {api.ResolvePath(mountPoint)} ({count} files, {mode})");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"mount: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("mount - mount a host zip archive into the VFS");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  mount                                  List active mounts");
        Console.WriteLine("  mount [options] <host-path> <mount-point>  Mount a zip archive");
        Console.WriteLine("  mount --sync <mount-point>             Save changes back to host zip");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -w, --rw      Writable mode: auto-save changes to the host zip");
        Console.WriteLine("  -s, --sync    Save in-memory changes to the host zip");
        Console.WriteLine("  -h, --help    Show this help");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  mount C:\\Users\\me\\data.zip /mnt/data            (read-only)");
        Console.WriteLine("  mount --rw C:\\Users\\me\\data.zip /mnt/data       (auto-save)");
        Console.WriteLine("  echo \"new content\" > /mnt/data/notes.txt");
        Console.WriteLine("  mount --sync /mnt/data                          (manual save)");
        Console.WriteLine("  umount --save /mnt/data                         (save + unmount)");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Only root can mount/unmount/sync archives.");
        Console.WriteLine("  Without --rw, changes are in-memory only.");
        Console.WriteLine("  With --rw, every file write/create/delete is auto-saved.");
    }
}
