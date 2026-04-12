================================================================================
                          NetNIX — Readme & User Guide
              A .NET-powered multi-user UNIX-like virtual environment
================================================================================

TABLE OF CONTENTS
  1. What is NetNIX?
  2. Getting Started
  3. First-Run Setup
  4. Logging In
  5. The Shell (nsh)
  6. Command Reference
  7. Users, Groups & Permissions
  8. sudo — Running Commands as Root
  9. The File System
 10. Output Redirection
 11. Shell Scripts (.nshrc & source)
 12. Writing C# Scripts
 13. The #include System
 14. Sandbox Security
 15. Mounting Host Archives (mount & umount)
 16. Importing & Exporting Files
 17. The Package Manager (npak)
 18. The Built-in Editor
 19. Getting Help (help & man)
 20. Resetting the Environment
 21. FAQ
 22. Troubleshooting

================================================================================
1. WHAT IS NETNIX?
================================================================================

NetNIX is a self-contained, virtual UNIX-like operating environment that runs
inside a .NET 8 console application. It provides:

  - A virtual file system stored in a single zip archive (rootfs.zip)
  - Multi-user login with password authentication
  - A UNIX-style shell (nsh) with builtins and script commands
  - User/group permissions modeled after Linux
  - Dynamically compiled C# scripts as executable commands
  - A package manager (npak) for installing apps and libraries
  - Host filesystem mounting, import, and export capabilities
  - A security sandbox preventing scripts from accessing the host directly

Everything runs in-memory. The virtual filesystem is persisted to:
  %APPDATA%\NetNIX\rootfs.zip

================================================================================
2. GETTING STARTED
================================================================================

  1. Run NetNIX.exe (or dotnet run).
  2. On first launch, the setup wizard runs automatically.
  3. Create a root password when prompted.
  4. Optionally create a regular user (recommended).
  5. Optionally add that user to the sudo group.
  6. After setup, you are presented with a login prompt.

================================================================================
3. FIRST-RUN SETUP
================================================================================

The first time you run NetNIX, the setup wizard:

  - Creates the standard directory tree (/bin, /etc, /home, /tmp, etc.)
  - Installs built-in commands and libraries
  - Installs manual pages
  - Creates the root user account
  - Creates the sudo group
  - Optionally creates a regular user
  - Optionally adds the regular user to the sudo group
  - Installs the sandbox security configuration
  - Saves everything to rootfs.zip

================================================================================
4. LOGGING IN
================================================================================

At the login prompt, enter your username and password:

  --- NetNIX Login ---
  login: alice
  password: ****

  - Root login uses username "root"
  - Passwords are masked while typing
  - Type "exit" or "logout" to return to the login screen

================================================================================
5. THE SHELL (nsh)
================================================================================

The NetNIX Shell (nsh) is an interactive command line similar to bash.

Prompt format:
  alice@netnix:/home/alice$     (regular user)
  root@netnix:/root#            (root user)

Features:
  - Tab-style command execution
  - Output redirection (> and >>)
  - Shell scripts (source/.)
  - Startup scripts (~/.nshrc)
  - Built-in commands and dynamically compiled .cs scripts

================================================================================
6. COMMAND REFERENCE
================================================================================

SHELL BUILTINS (always available):
  help          Show available commands
  man <topic>   View manual pages
  man --list    List all available man pages
  cd <dir>      Change directory
  edit <file>   Open the built-in text editor
  write <file>  Quick file write (type content, end with blank line)
  chmod         Change file permissions
  chown         Change file owner
  stat <file>   Display file details
  tree [dir]    Show directory tree
  adduser       Create a new user
  deluser       Delete a user
  passwd        Change a user's password
  su <user>     Switch to another user account
  sudo <cmd>    Run a command as root
  users         List all users
  groups        List all groups
  run <file>    Compile and run a .cs script
  source <file> Execute a shell script (alias: .)
  clear         Clear the screen
  exit/logout   Log out

SCRIPT COMMANDS (installed in /bin):
  File operations:    ls  cat  cp  mv  rm  mkdir  rmdir  touch
  Text processing:    head  tail  wc  grep  find  tee  echo
  Path utilities:     pwd  basename  dirname
  System info:        whoami  id  uname  hostname  date  env
  Disk utilities:     du  df
  Miscellaneous:      yes  true  false
  Network:            curl  wget  fetch
  Clipboard:          cbpaste  cbcopy
  Archives:           zip  unzip
  Host interaction:   mount  umount  export  importfile
  Package manager:    npak  npak-demo
  User management:    useradd  userdel  usermod
  Group management:   groupadd  groupdel  groupmod
  Demo:               demo  npak-demo

================================================================================
7. USERS, GROUPS & PERMISSIONS
================================================================================

NetNIX uses a multi-user permission model similar to Linux.

USERS:
  - root (uid 0) is the superuser with full access
  - Regular users have limited permissions
  - User data: /etc/passwd, /etc/shadow

  Creating users (as root):
    adduser bob
    useradd bob mypassword

  Deleting users:
    deluser bob

  Changing passwords:
    passwd              (change your own)
    passwd bob          (root can change anyone's)

GROUPS:
  - Each user has a primary group
  - Users can belong to additional groups (e.g., sudo)
  - Group data: /etc/group

  Managing groups (as root):
    groupadd developers
    usermod -G developers bob
    groupdel developers

PERMISSIONS:
  Files have owner, group, and permission strings like Linux:
    rwxr-xr-x    (owner: rwx, group: r-x, others: r-x)

  Changing permissions:
    chmod 755 /bin/myscript.cs
    chmod rw-r--r-- notes.txt

  Changing ownership:
    chown alice myfile.txt

================================================================================
8. SUDO — RUNNING COMMANDS AS ROOT
================================================================================

Regular users in the "sudo" group can execute commands as root:

  alice$ sudo mount extra.zip /mnt/extra
  [sudo] password for alice: ****
  mount: mounted extra.zip at /mnt/extra (12 files, ro)

  - You must be a member of the "sudo" group
  - You authenticate with YOUR OWN password (not root's)
  - After the command, privileges return to normal

Adding a user to the sudo group (as root):
  usermod -G sudo alice

================================================================================
9. THE FILE SYSTEM
================================================================================

NetNIX uses a virtual file system (VFS) with a standard UNIX directory layout:

  /               Root directory
  /bin            System commands
  /etc            Configuration files
  /etc/passwd     User accounts
  /etc/shadow     Password hashes
  /etc/group      Group definitions
  /etc/motd       Message of the day
  /etc/sandbox.conf  Script sandbox rules
  /etc/skel       Default files for new users
  /home           User home directories
  /home/alice     Alice's home directory
  /lib            System libraries
  /mnt            Mount points for host archives
  /root           Root's home directory
  /tmp            Temporary files
  /usr/local/bin  User-installed commands (from npak)
  /usr/local/lib  User-installed libraries (from npak)
  /usr/share/man  Manual pages
  /var/lib/npak   Package manager database

The entire VFS is stored in a single zip archive:
  %APPDATA%\NetNIX\rootfs.zip

Changes are saved automatically on logout, or manually with the
save command in scripts (api.Save()).

================================================================================
10. OUTPUT REDIRECTION
================================================================================

Redirect command output to files:

  echo "hello" > /tmp/greeting.txt        (overwrite)
  echo "world" >> /tmp/greeting.txt       (append)
  ls /bin > /tmp/commands.txt

================================================================================
11. SHELL SCRIPTS (.nshrc & source)
================================================================================

Shell scripts contain one command per line:

  # my-script.sh
  echo "Starting backup..."
  cp /etc/passwd /tmp/passwd-backup
  echo "Done."

Run them with:
  source my-script.sh
  . my-script.sh

STARTUP SCRIPTS:
  ~/.nshrc runs automatically when you log in. Use it to set up
  your environment:

  # ~/.nshrc
  echo "Welcome back!"
  echo "Today is:"
  date

================================================================================
12. WRITING C# SCRIPTS
================================================================================

NetNIX commands are C# scripts compiled and executed on the fly.
Scripts must define a class with a static Run method:

  using System;
  using NetNIX.Scripting;

  public static class MyCommand
  {
      public static int Run(NixApi api, string[] args)
      {
          Console.WriteLine("Hello from my script!");
          Console.WriteLine($"Running as: {api.Username} (uid {api.Uid})");
          Console.WriteLine($"Working dir: {api.Cwd}");

          // Read/write VFS files through the api
          api.WriteText("/tmp/test.txt", "file content");
          string content = api.ReadText("/tmp/test.txt");

          return 0; // 0 = success
      }
  }

Save as a .cs file and run:
  run myscript.cs              (run from current directory)
  cp myscript.cs /bin/         (install globally)
  myscript                     (now runs as a command)

The NixApi provides file, user, network, zip, and other operations.
See "man api" for the full reference.

IMPORTANT: Scripts run inside a security sandbox. Direct use of
System.IO, System.Net, System.Diagnostics, and other dangerous
namespaces is blocked. All host interaction must go through the
NixApi. See section 14 (Sandbox Security) for details.

================================================================================
13. THE #include SYSTEM
================================================================================

Scripts can include library files:

  #include <netlib>            Search /lib, /usr/lib, /usr/local/lib
  #include "mylib.cs"          Relative to current directory

Libraries are .cs files containing utility classes. Built-in libs:
  netlib     — HTTP/networking helpers
  ziplib     — Zip archive utilities

See "man include" for details.

================================================================================
14. SANDBOX SECURITY
================================================================================

User scripts are sandboxed to prevent access to the host system.
The sandbox has three layers:

  1. BLOCKED USINGS — "using System.IO;" and similar are rejected
  2. BLOCKED TOKENS — patterns like "File.WriteAll" are caught
  3. ASSEMBLY ALLOWLIST — only safe .NET assemblies are referenced

Configuration is stored in /etc/sandbox.conf (root-editable):

  [blocked_usings]
  System.IO
  System.Diagnostics
  System.Net
  System.Reflection
  ...

  [blocked_tokens]
  File.WriteAll
  Directory.Create
  Process.Start
  ...

If a script is blocked:
  nsh: exploit.cs: blocked — 'using System.IO' is not permitted
    Namespace 'System.IO' is blocked by /etc/sandbox.conf
    Scripts must use the NixApi for all file, network, and system operations.
    Root can edit /etc/sandbox.conf to modify sandbox rules.

Root can edit rules:   edit /etc/sandbox.conf
See "man sandbox" for full documentation.

================================================================================
15. MOUNTING HOST ARCHIVES (mount & umount)
================================================================================

Mount zip archives from the host OS into the VFS:

  mount extra.zip /mnt/extra                (read-only)
  mount --rw extra.zip /mnt/extra           (writable, auto-save)
  mount                                     (list active mounts)
  mount --sync /mnt/extra                   (manually save changes)
  umount /mnt/extra                         (discard changes)
  umount --save /mnt/extra                  (save and unmount)

Mount modes:
  ro (default) — Changes held in memory. Use --sync or umount --save
                 to write back. Changes lost if you don't save.
  rw (--rw)    — Every file change is automatically saved to the
                 host zip immediately.

Only root can mount/unmount. Non-root users see:
  mount: permission denied (must be root)

Use sudo:
  sudo mount extra.zip /mnt/extra

================================================================================
16. IMPORTING & EXPORTING FILES
================================================================================

IMPORT a file from the host OS into the VFS:
  importfile C:\Users\me\data.zip                     (to current dir)
  importfile C:\Users\me\data.zip /tmp/data.zip       (to specific path)
  importfile C:\Users\me\notes.txt /home/alice/        (into a directory)

EXPORT the VFS to a zip on the host OS:
  export C:\Backups\netnix.zip                         (entire VFS)
  export C:\Backups\home.zip /home/alice               (subtree only)
  export --mounts C:\Backups\everything.zip            (include mounts)

  By default, mounted filesystems (/mnt) are excluded from exports.
  Use --mounts to include them.

Both commands are root-only. Use sudo if needed.

================================================================================
17. THE PACKAGE MANAGER (npak)
================================================================================

npak installs, removes, and manages packages for all users.

COMMANDS:
  npak install /path/to/package.npak    Install a package
  npak remove <name>                    Uninstall a package
  npak list                             List installed packages
  npak info <name>                      Show package details

PACKAGE FORMAT (.npak):
  A .npak file is a zip archive containing:

  manifest.txt      Required. Package metadata:
                       name=hello
                       version=1.0
                       description=A hello world app
                       type=app

  bin/               Scripts installed to /usr/local/bin/
  lib/               Libraries installed to /usr/local/lib/
  man/               Man pages installed to /usr/share/man/

DEMO:
  Run "npak-demo" as root for an interactive walkthrough that
  creates, installs, runs, and removes a sample package:

    npak-demo

  After the demo, the example package remains at /tmp/hello.npak
  so you can inspect it:

    unzip -l /tmp/hello.npak

CREATING YOUR OWN PACKAGE:
  1. Create a directory structure:
       mkdir /tmp/myapp
       mkdir /tmp/myapp/bin
       mkdir /tmp/myapp/man

  2. Write a manifest:
       echo "name=myapp" > /tmp/myapp/manifest.txt
       echo "version=1.0" >> /tmp/myapp/manifest.txt
       echo "description=My application" >> /tmp/myapp/manifest.txt
       echo "type=app" >> /tmp/myapp/manifest.txt

  3. Add your script:
       cp myapp.cs /tmp/myapp/bin/myapp.cs

  4. Build the .npak:
       zip /tmp/myapp.npak /tmp/myapp

  5. Install:
       npak install /tmp/myapp.npak

Only root can install/remove packages. Use sudo if needed.

================================================================================
18. THE BUILT-IN EDITOR
================================================================================

NetNIX includes a simple text editor for creating and modifying files:

  edit /etc/motd
  edit ~/myscript.cs

Editor commands (shown at the bottom of the editor):
  Type text normally, one line at a time.
  The editor saves when you exit.

For quick one-off writes without the editor:
  write /tmp/note.txt
  (type content, press Enter on a blank line to save)

Or use echo with redirection:
  echo "hello world" > /tmp/note.txt

================================================================================
19. GETTING HELP (help & man)
================================================================================

  help                  Show all available commands
  man <command>         View the manual page for a command
  man --list            List all available manual pages

Available help topics:
  man api               NixApi scripting reference
  man scripting         How to write .cs scripts
  man include           Library #include system
  man editor            Text editor guide
  man filesystem        Filesystem hierarchy
  man permissions       File permission system
  man sandbox           Script sandbox security
  man nshrc             Shell startup scripts
  man npak              Package manager documentation

Every built-in and script command has a man page. Use -h or --help
with most commands for quick usage info:
  ls -h
  mount --help
  npak --help

================================================================================
20. RESETTING THE ENVIRONMENT
================================================================================

If you need to start fresh, there are two ways to reset:

METHOD 1 — Boot-time reset:
  When NetNIX starts, you have 3 seconds to press Ctrl+R.
  Type "YES" to confirm. This deletes rootfs.zip and re-runs setup.

METHOD 2 — Login-screen reset:
  At the login prompt, enter username: __reset__
  Type "YES" to confirm.

Both methods completely erase the virtual filesystem and all user
data. You will go through first-run setup again.

MANUAL RESET:
  Delete the file %APPDATA%\NetNIX\rootfs.zip and restart NetNIX.

================================================================================
21. FAQ
================================================================================

Q: Where is my data stored?
A: Everything is in %APPDATA%\NetNIX\rootfs.zip — a single zip file.

Q: Can scripts access my real files?
A: No. Scripts run inside a security sandbox that blocks System.IO,
   System.Net, System.Diagnostics, and other dangerous namespaces.
   All file/network access must go through the NixApi. Root can
   adjust sandbox rules in /etc/sandbox.conf.

Q: How do I get files into NetNIX?
A: Use "importfile" to copy a host file into the VFS, or "mount" to
   mount a zip archive. Both require root/sudo.

Q: How do I get files out of NetNIX?
A: Use "export" to write the VFS (or a subtree) to a host zip file.
   Requires root/sudo.

Q: How do I install new commands?
A: Write a .cs script and copy it to /bin/:
     cp myscript.cs /bin/
   Or create a .npak package and install it:
     npak install mypackage.npak

Q: I forgot my root password. How do I recover?
A: Reset the environment (see section 20). This erases everything.
   There is no password recovery.

Q: How do I add a user to the sudo group?
A: As root: usermod -G sudo <username>
   Or during first-run setup, choose "y" when prompted.

Q: Why does my script say "blocked"?
A: Your script uses a namespace or API pattern that is blocked by
   the sandbox (/etc/sandbox.conf). Use the NixApi instead of
   direct .NET APIs. See "man sandbox" and "man api".

Q: Can I run NetNIX on Linux or macOS?
A: NetNIX is a .NET 8 application and should run on any platform
   that supports the .NET 8 runtime. The data directory will be
   in the appropriate AppData/config location for your OS.

Q: How do I update built-in commands after an update?
A: Reset the environment (section 20) to get the latest builtins,
   or manually copy updated .cs files into /bin/.

Q: What happens if I mount with --rw and my app crashes?
A: In --rw mode, changes are written to the host zip after every
   mutation. Data up to the last completed operation is saved.

================================================================================
22. TROUBLESHOOTING
================================================================================

PROBLEM: "command not found"
  - Check spelling. Run "help" to see available commands.
  - If it's a script, make sure it's in /bin, /usr/bin, /usr/local/bin,
    or the current directory.
  - Check file permissions with "stat <file>".

PROBLEM: "permission denied"
  - You may need to be root. Use "sudo <command>" or "su root".
  - Check file permissions: stat <file>, chmod, chown.

PROBLEM: Script compilation errors
  - Check the error messages — they include line numbers.
  - Make sure you have the correct "using" statements.
  - Remember: System.IO, System.Net, etc. are blocked by the sandbox.
    Use NixApi methods instead.

PROBLEM: "blocked by /etc/sandbox.conf"
  - Your script uses a forbidden namespace or API.
  - Use api.ReadText(), api.WriteText(), api.Net.Get(), etc.
  - See "man api" for the full list of safe operations.

PROBLEM: mount says "no such file on host"
  - Check the host path. It must be a full path to a .zip file
    that exists on your real filesystem.
  - Use importfile to copy a file into the VFS first if needed.

PROBLEM: "not a mount point"
  - The path you specified isn't an active mount. Use "mount" with
    no arguments to list active mounts.

PROBLEM: Need to start over
  - See section 20 (Resetting the Environment).

================================================================================
                            Thank you for using NetNIX!
                       Report issues at the project repository.
================================================================================
