# NetNIX

A multi-user UNIX-like operating environment built entirely in C# on .NET 8. NetNIX simulates a complete UNIX experience inside your terminal — with a virtual filesystem, user accounts, a shell, file permissions, a text editor, networking, and a C# scripting engine that lets you write and run scripts directly inside the environment.

Everything lives in a single portable zip archive on your host OS. No admin rights, no VMs, no containers.

## Features

### Virtual Filesystem
- Full UNIX directory hierarchy (`/bin`, `/etc`, `/home`, `/usr`, `/var`, `/tmp`, etc.)
- Backed by a single `.zip` archive stored in your OS `AppData` folder
- Supports files and directories with metadata (owner, group, permissions)
- Standard path operations: absolute, relative, `.`, `..`, `~`

### Multi-User System
- `/etc/passwd` and `/etc/shadow` based user/group management
- SHA-256 password hashing
- Root account (uid 0) with full system access
- Standard user accounts with isolated home directories (`/home/<user>`)
- Per-user and per-group primary groups
- User locking/unlocking
- Login prompt with masked password input

### UNIX File Permissions
- Full `rwxrwxrwx` (owner/group/other) permission model
- Enforced on all file and directory operations (read, write, create, delete, move, copy, list)
- Directory traverse (execute) permission checking along the full path
- `chmod` with symbolic (`rwxr-xr-x`) or octal (`755`) notation
- `chown` for changing file ownership (root only)
- Home directories default to `rwx------` (700) — private to each user
- Root bypasses all permission checks

### Interactive Shell (nsh)
- Prompt shows `user@netnix:/path$` (or `#` for root)
- Quoting support (single and double quotes)
- Output redirection (`>` and `>>`) with permission checks
- Shell variable expansion: `$USER`, `$UID`, `$GID`, `$HOME`, `$PWD`, `$CWD`, `$SHELL`, `$HOSTNAME`, `~`
- Startup scripts (`~/.nshrc`) run automatically on login
- `source` / `.` to run shell scripts (one command per line)
- Shell scripts with `#!/bin/nsh` shebang support
- Script search path: current directory → `/bin` → `/usr/bin` → `/usr/local/bin`

### Built-in Shell Commands
| Command | Description |
|---------|-------------|
| `help` | Show command summary |
| `man` | Display manual pages (`man <topic>`, `man -k <keyword>`, `man --list`) |
| `cd` | Change directory (`cd`, `cd ~`, `cd -`, `cd ..`) |
| `edit` | Full-screen text editor (nano-inspired) |
| `write` | Write text to a file interactively |
| `chmod` | Change file permissions |
| `chown` | Change file ownership (root only) |
| `stat` | Display file/directory metadata |
| `tree` | Display directory tree |
| `adduser` | Create a new user (root only) |
| `deluser` | Delete a user (root only) |
| `passwd` | Change passwords |
| `su` | Switch user |
| `users` | List all users |
| `groups` | List all groups |
| `run` | Run a C# script file |
| `source` / `.` | Execute a shell script |
| `clear` | Clear the screen |
| `exit` / `logout` | End the session |

### Script Commands (`/bin/*.cs`)
All of these are plain C# source files compiled and executed at runtime:

| Category | Commands |
|----------|----------|
| **Files** | `ls`, `cat`, `cp`, `mv`, `rm`, `mkdir`, `rmdir`, `touch` |
| **Text** | `head`, `tail`, `wc`, `grep`, `find`, `tee`, `echo` |
| **Navigation** | `pwd`, `basename`, `dirname` |
| **Disk** | `du`, `df` |
| **User Info** | `whoami`, `id`, `env` |
| **System** | `uname`, `hostname`, `date`, `yes`, `true`, `false` |
| **Networking** | `curl`, `wget`, `fetch` |
| **Clipboard** | `cbcopy`, `cbpaste` |
| **Archives** | `zip`, `unzip` |
| **User Mgmt** | `useradd`, `userdel`, `usermod`, `groupadd`, `groupdel`, `groupmod` |

### Text Editor (nedit)
- Full-screen nano-style editor launched with `edit <file>`
- Line numbers, cursor movement, scrolling
- Insert, delete, save with `Ctrl+S`/'Ctrl+W, quit with `Ctrl+Q`
- Permission-aware — checks read/write access before loading/saving

### C# Scripting Engine
- Write scripts as plain `.cs` files stored anywhere in the VFS
- Scripts define a class with `static int Run(NixApi api, string[] args)`
- Compiled at runtime using Roslyn (`Microsoft.CodeAnalysis.CSharp`)
- Compiled assemblies are cached in memory for performance
- Rich `NixApi` surface for filesystem, user, networking, and archive operations
- `#include <libname>` / `#include "path"` preprocessor for shared libraries
- Libraries stored in `/lib`, `/usr/lib`, `/usr/local/lib`
- Built-in libraries: `netlib`, `ziplib`, `demoapilib`

### Networking
- HTTP GET, POST, and form-POST from scripts via `api.Net`
- `curl`, `wget`, `fetch` commands for downloading content
- `api.Download()` / `api.DownloadText()` to save URLs to the VFS

### Zip Archive Support
- Create and extract zip archives within the VFS
- `zip` / `unzip` commands
- `api.ZipCreate()` / `api.ZipExtract()` from scripts

### Manual Pages
- Comprehensive built-in `man` pages for all commands and topics
- Searchable with `man -k <keyword>`
- Users can create their own pages in `/usr/share/man/`
- Topics include: `api`, `scripting`, `include`, `editor`, `filesystem`, `permissions`, `nshrc`

### Environment Reset
- Press `Ctrl+R` during the 3-second boot window to reset the entire environment
- Or type `__reset__` at the login prompt
- Wipes the filesystem archive and re-runs first-time setup

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run
```bash
git clone https://github.com/<your-username>/NetNIX.git
cd NetNIX
dotnet run --project NetNIX
```

### First Run
On first launch, NetNIX runs a setup wizard that:
1. Creates the standard UNIX directory hierarchy
2. Installs default shell startup scripts and skeleton files (`/etc/skel`)
3. Prompts you to set a **root password**
4. Optionally creates a **regular user** account
5. Installs all built-in commands in `/bin`
6. Installs shared libraries in `/lib`
7. Installs manual pages in `/usr/share/man`
8. Saves everything to `%APPDATA%/NetNIX/rootfs.zip`

### Quick Tour
```
root@netnix:/root# help                    # see all commands
root@netnix:/root# man ls                  # read the ls manual page
root@netnix:/root# ls /                    # list root directory
root@netnix:/root# adduser alice           # create a user
root@netnix:/root# su alice                # switch to alice
alice@netnix:/home/alice$ whoami            # confirm identity
alice@netnix:/home/alice$ echo "hello" > greeting.txt
alice@netnix:/home/alice$ cat greeting.txt
alice@netnix:/home/alice$ edit notes.txt   # open the text editor
alice@netnix:/home/alice$ tree ~           # show home directory tree
alice@netnix:/home/alice$ man scripting    # learn to write C# scripts
alice@netnix:/home/alice$ exit             # log out
```

### Writing a C# Script
Create a file (e.g. `edit ~/hello.cs`) with:
```csharp
using System;
using NetNIX.Scripting;

public static class HelloCommand
{
    public static int Run(NixApi api, string[] args)
    {
        Console.WriteLine($"Hello from {api.Username}! You are in {api.Cwd}");
        return 0;
    }
}
```
Then run it:
```
alice@netnix:~$ run ~/hello.cs
```

Or place it in `/bin` and run it by name (root required to write to `/bin`):
```
root@netnix:~# cp /home/alice/hello.cs /bin/hello.cs
root@netnix:~# hello          # now available as a command
```

### Using Libraries
Create a shared library in `/lib` and include it in scripts:
```csharp
#include <netlib>
// Now you can use functions from /lib/netlib.cs
```

## Data Storage
All filesystem state is persisted in a single zip archive at:
```
%APPDATA%/NetNIX/rootfs.zip        (Windows)
~/.config/NetNIX/rootfs.zip        (Linux/macOS)
```

File metadata (owners, groups, permissions) is stored in a `.vfsmeta` entry inside the archive.

## Project Structure
```
NetNIX/
├── Program.cs                  # Entry point — boot, login loop, reset
├── Shell/
│   ├── NixShell.cs             # Interactive shell (nsh)
│   └── TextEditor.cs           # Full-screen text editor (nedit)
├── VFS/
│   ├── VirtualFileSystem.cs    # Zip-backed virtual filesystem
│   └── VfsNode.cs              # File/directory node with permissions
├── Users/
│   ├── UserManager.cs          # User & group CRUD, /etc/passwd, /etc/shadow
│   ├── UserRecord.cs           # User account model
│   └── GroupRecord.cs          # Group model
├── Scripting/
│   ├── ScriptRunner.cs         # Roslyn-based C# script compiler & runner
│   ├── NixApi.cs               # API surface exposed to scripts
│   └── NixNet.cs               # Networking API (HTTP client)
├── Setup/
│   ├── FirstRunSetup.cs        # First-run wizard
│   ├── BuiltinScripts.cs       # Loader for /bin commands
│   ├── BuiltinLibs.cs          # Loader for /lib libraries
│   └── HelpPages.cs            # Manual page content
├── Builtins/                   # C# source for all /bin commands (not compiled into the app)
│   ├── ls.cs, cat.cs, grep.cs, curl.cs, ...
└── Libs/                       # C# source for shared libraries
    ├── netlib.cs, ziplib.cs, demoapilib.cs
```

> **Note:** Files in `Builtins/` and `Libs/` are **not** compiled as part of the .NET project. They are copied to the output directory as content files and installed into the VFS at first run, where they are compiled at runtime by the scripting engine.

## Technology
- **.NET 8** console application
- **Roslyn** (`Microsoft.CodeAnalysis.CSharp`) for runtime C# script compilation
- **System.IO.Compression** for the zip-backed virtual filesystem
- No external dependencies beyond the Roslyn compiler package

## License
See [LICENSE](LICENSE) for details.
