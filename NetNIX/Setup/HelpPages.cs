namespace NetNIX.Setup;

/// <summary>
/// Contains the plain-text manual pages installed into /usr/share/man/
/// during first-run setup. Users can add their own pages by creating
/// files in /usr/share/man/.
/// </summary>
public static class HelpPages
{
    // ?? Shell builtins ?????????????????????????????????????????????

    public const string Man = """
        MAN(1)                    NetNIX Manual                    MAN(1)

        NAME
            man - display manual pages

        SYNOPSIS
            man <topic>
            man -k <keyword>
            man --list

        DESCRIPTION
            Display the manual page for a command or topic.

            Pages are stored as plain text in /usr/share/man/. You can
            add your own pages by creating files there:

                edit /usr/share/man/mycommand.txt

        OPTIONS
            man <topic>       Show the manual page for <topic>
            man -k <keyword>  Search all pages for a keyword
            man --list        List all available manual pages

        TOPICS
            Any command name (ls, cat, grep, etc.)
            api               NixApi scripting reference
            scripting         How to write .cs scripts
            editor            Text editor guide
            filesystem        Filesystem hierarchy
            permissions       File permission system

        SEE ALSO
            help, cat
        """;

    public const string Help = """
        HELP(1)                   NetNIX Manual                   HELP(1)

        NAME
            help - show command summary

        SYNOPSIS
            help

        DESCRIPTION
            Displays a brief list of all available shell builtins and
            script commands.

            For detailed help on any command, use:
                man <command>

        SEE ALSO
            man
        """;

    public const string Cd = """
        CD(1)                     NetNIX Manual                     CD(1)

        NAME
            cd - change working directory

        SYNOPSIS
            cd [dir]
            cd ~
            cd -

        DESCRIPTION
            Change the current working directory.

            cd              Go to home directory
            cd ~            Go to home directory
            cd <dir>        Go to specified directory
            cd -            Go to previous directory (simplified)
            cd ..           Go up one level
            cd /            Go to root

        EXAMPLES
            cd /home/user
            cd ..
            cd ~/projects

        SEE ALSO
            pwd, ls
        """;

    public const string Edit = """
        EDIT(1)                   NetNIX Manual                   EDIT(1)

        NAME
            edit - full-screen text editor

        SYNOPSIS
            edit <file>

        DESCRIPTION
            Opens a file in the built-in text editor (nedit). If the file
            does not exist, it will be created on first save.

        KEYBOARD SHORTCUTS
            F2 / Ctrl+W     Save file
            Ctrl+Q          Quit (prompts if unsaved changes)
            Ctrl+T          Insert C# template
            Ctrl+G          Go to line number
            Ctrl+K          Cut current line to clipboard
            Ctrl+U          Paste line from clipboard

            Arrow keys      Move cursor
            Home / End      Beginning / end of line
            Page Up/Down    Scroll by screen
            Tab             Insert 4 spaces
            Enter           New line (auto-indents)

        C# TEMPLATES (Ctrl+T)
            1 - Script      Complete NixApi script with Run() entry point
            2 - Class       Basic class with constructor
            3 - Main        Script with argument parsing boilerplate
            4 - Snippet     Common NixApi usage patterns

        EXAMPLES
            edit hello.cs          Create a new script
            edit /bin/ls.cs        Edit an existing command
            edit /etc/motd         Edit system message

        SEE ALSO
            write, run, man scripting
        """;

    public const string Write = """
        WRITE(1)                  NetNIX Manual                  WRITE(1)

        NAME
            write - write text to a file interactively

        SYNOPSIS
            write <file>

        DESCRIPTION
            Opens an interactive line-by-line text input. Type your
            content, then enter a single '.' on a line to finish and
            save. Creates the file if it doesn't exist.

            For a full editor experience, use 'edit' instead.

        EXAMPLES
            write notes.txt
            > This is line one
            > This is line two
            > .

        SEE ALSO
            edit, echo, tee
        """;

    public const string Chmod = """
        CHMOD(1)                  NetNIX Manual                  CHMOD(1)

        NAME
            chmod - change file permissions

        SYNOPSIS
            chmod <mode> <path>

        DESCRIPTION
            Change the permissions of a file or directory. Only the
            file owner or root can change permissions.

        MODES
            String format:  rwxr-xr-x  (9 characters, owner/group/other)
            Octal format:   755         (3 digits)

            Permission bits:
              r = read (4)    w = write (2)    x = execute (1)

        EXAMPLES
            chmod rwxr-xr-x /bin/myscript.cs
            chmod 755 /bin/myscript.cs
            chmod 644 notes.txt

        SEE ALSO
            chown, stat, man permissions
        """;

    public const string Chown = """
        CHOWN(1)                  NetNIX Manual                  CHOWN(1)

        NAME
            chown - change file owner

        SYNOPSIS
            chown <user> <path>

        DESCRIPTION
            Change the owner of a file or directory. Only root can
            use this command.

        EXAMPLES
            chown alice /home/alice/file.txt

        SEE ALSO
            chmod, stat
        """;

    public const string Adduser = """
        ADDUSER(1)                NetNIX Manual                ADDUSER(1)

        NAME
            adduser - create a new user account

        SYNOPSIS
            adduser <username>

        DESCRIPTION
            Create a new user account with a home directory. Prompts
            for a password. Only root can create users.

            The new user gets:
              - A unique UID
              - A personal group with matching GID
              - A home directory at /home/<username>

        SEE ALSO
            deluser, passwd, users, su
        """;

    public const string Deluser = """
        DELUSER(1)                NetNIX Manual                DELUSER(1)

        NAME
            deluser - delete a user account

        SYNOPSIS
            deluser <username>

        DESCRIPTION
            Remove a user account and their personal group. Only root
            can delete users. The home directory is NOT removed.

        SEE ALSO
            adduser, users
        """;

    public const string Passwd = """
        PASSWD(1)                 NetNIX Manual                 PASSWD(1)

        NAME
            passwd - change user password

        SYNOPSIS
            passwd [username]

        DESCRIPTION
            Change the password for a user. Regular users can only
            change their own password. Root can change any password.

            passwd          Change own password
            passwd alice    Change alice's password (root only)

        SEE ALSO
            su, adduser
        """;

    public const string Su = """
        SU(1)                     NetNIX Manual                     SU(1)

        NAME
            su - switch user

        SYNOPSIS
            su <username>

        DESCRIPTION
            Switch to another user account. Requires the target user's
            password unless you are root.

        EXAMPLES
            su root
            su alice

        SEE ALSO
            whoami, id, passwd, sudo
        """;

    public const string Sudo = """
        SUDO(8)                   NetNIX Manual                   SUDO(8)

        NAME
            sudo - execute a command as root

        SYNOPSIS
            sudo <command> [args...]

        DESCRIPTION
            Executes the given command with root (uid 0) privileges.
            The calling user must be a member of the 'sudo' group.

            When invoked, sudo prompts for the calling user's own
            password (not the root password) to verify identity.

            Root users do not need sudo — the command is executed
            directly without a password prompt.

        SETUP
            To grant a user sudo access:

                su root
                usermod -G sudo <username>

            Or during first-run setup, choose 'y' when asked to add
            the user to the sudo group.

        EXAMPLES
            sudo mount extra.zip /mnt/extra
            sudo useradd newuser password123
            sudo chmod 755 /bin/myscript.cs
            sudo rm /etc/somefile

        NOTES
            Only members of the 'sudo' group can use this command.
            The user authenticates with their own password.
            The command runs with uid=0, gid=0 (root).
            After the command finishes, privileges return to normal.

        SEE ALSO
            su, passwd, usermod, groups
        """;

    public const string Stat = """
        STAT(1)                   NetNIX Manual                   STAT(1)

        NAME
            stat - display file details

        SYNOPSIS
            stat <path>

        DESCRIPTION
            Show detailed information about a file or directory:
            path, type, size, permissions, owner, and group.

        EXAMPLES
            stat /etc/motd
            stat /bin/ls.cs

        SEE ALSO
            ls, chmod, chown
        """;

    public const string Tree = """
        TREE(1)                   NetNIX Manual                   TREE(1)

        NAME
            tree - display directory tree

        SYNOPSIS
            tree [dir]

        DESCRIPTION
            Show a recursive tree view of a directory and all its
            contents. Defaults to the current directory.

        EXAMPLES
            tree
            tree /usr
            tree /home

        SEE ALSO
            ls, find
        """;

    public const string Run = """
        RUN(1)                    NetNIX Manual                    RUN(1)

        NAME
            run - compile and execute a .cs script

        SYNOPSIS
            run <file.cs> [args...]

        DESCRIPTION
            Compile a C# script file and execute it. The script must
            contain a class with:

                static int Run(NixApi api, string[] args)

            Scripts are compiled with Roslyn at runtime. Compiled
            assemblies are cached in memory for faster re-execution.

        EXAMPLES
            run hello.cs
            run /home/user/tool.cs --verbose

        NOTES
            Scripts in the current directory or /bin can also be run
            by name without 'run':
                hello           runs ./hello.cs or /bin/hello.cs

        SEE ALSO
            edit, man scripting, man api
        """;

    public const string Users = """
        USERS(1)                  NetNIX Manual                  USERS(1)

        NAME
            users - list all user accounts

        SYNOPSIS
            users

        DESCRIPTION
            Display all user accounts with their UID, GID, group name,
            and home directory.

        SEE ALSO
            groups, id, adduser
        """;

    public const string Groups = """
        GROUPS(1)                 NetNIX Manual                 GROUPS(1)

        NAME
            groups - list all groups

        SYNOPSIS
            groups

        DESCRIPTION
            Display all groups with their GID and member list.

        SEE ALSO
            users, id
        """;

    public const string Useradd = """
        USERADD(8)                NetNIX Manual                USERADD(8)

        NAME
            useradd - create a new user account

        SYNOPSIS
            useradd [options] <username>

        DESCRIPTION
            Create a new user account with a home directory, personal
            group, and default shell. Only root can run this command.

        OPTIONS
            -m, --create-home       Create home directory (default)
            -d, --home DIR          Set custom home directory path
            -s, --shell SHELL       Set login shell (default: /bin/nsh)
            -p, --password PASS     Set password (default: username)
            -G, --groups G1,G2      Add to supplementary groups

        EXAMPLES
            useradd alice
            useradd -p secret bob
            useradd -d /home/carol -s /bin/nsh carol
            useradd -G developers,staff dave

        NOTES
            Each new user automatically gets:
              - A unique UID
              - A personal primary group with matching GID
              - Files from /etc/skel copied to home directory

        SEE ALSO
            userdel, usermod, groupadd, adduser, passwd
        """;

    public const string Userdel = """
        USERDEL(8)                NetNIX Manual                USERDEL(8)

        NAME
            userdel - delete a user account

        SYNOPSIS
            userdel [options] <username>

        DESCRIPTION
            Remove a user account and their personal primary group.
            Only root can run this command.

        OPTIONS
            -r, --remove    Also delete the user's home directory
                            and all files within it

        EXAMPLES
            userdel alice
            userdel -r bob

        NOTES
            The root user cannot be deleted.
            Without -r, the home directory is preserved.

        SEE ALSO
            useradd, usermod, deluser
        """;

    public const string Usermod = """
        USERMOD(8)                NetNIX Manual                USERMOD(8)

        NAME
            usermod - modify a user account

        SYNOPSIS
            usermod [options] <username>

        DESCRIPTION
            Modify an existing user account. Change home directory,
            shell, group membership, or lock/unlock the account.
            Only root can run this command.

        OPTIONS
            -d, --home DIR               Change home directory
            -s, --shell SHELL            Change login shell
            -L, --lock                   Lock account (disable login)
            -U, --unlock                 Unlock account (enable login)
            -aG, --append-group G1,G2    Add to supplementary groups
            -rG, --remove-group G1,G2    Remove from supplementary groups

        EXAMPLES
            usermod -d /home/newhome alice
            usermod -s /bin/nsh bob
            usermod -L carol
            usermod -U carol
            usermod -aG developers,staff alice
            usermod -rG staff bob

        NOTES
            Locking prepends '!' to the password hash, preventing
            authentication. Unlocking removes the '!' prefix.

        SEE ALSO
            useradd, userdel, groupmod, passwd
        """;

    public const string Groupadd = """
        GROUPADD(8)               NetNIX Manual               GROUPADD(8)

        NAME
            groupadd - create a new group

        SYNOPSIS
            groupadd <groupname>

        DESCRIPTION
            Create a new group with an automatically assigned GID.
            Only root can run this command.

        EXAMPLES
            groupadd developers
            groupadd staff

        SEE ALSO
            groupdel, groupmod, usermod, groups
        """;

    public const string Groupdel = """
        GROUPDEL(8)               NetNIX Manual               GROUPDEL(8)

        NAME
            groupdel - delete a group

        SYNOPSIS
            groupdel <groupname>

        DESCRIPTION
            Delete an existing group. The group cannot be deleted if
            it is the primary group of any user. Only root can run
            this command.

        EXAMPLES
            groupdel developers

        SEE ALSO
            groupadd, groupmod, groups
        """;

    public const string Groupmod = """
        GROUPMOD(8)               NetNIX Manual               GROUPMOD(8)

        NAME
            groupmod - modify a group

        SYNOPSIS
            groupmod [options] <groupname>

        DESCRIPTION
            Modify an existing group. Currently supports renaming.
            Only root can run this command.

        OPTIONS
            -n, --new-name NAME     Rename the group

        EXAMPLES
            groupmod -n engineering developers

        SEE ALSO
            groupadd, groupdel, usermod, groups
        """;

    public const string Clear = """
        CLEAR(1)                  NetNIX Manual                  CLEAR(1)

        NAME
            clear - clear the screen

        SYNOPSIS
            clear

        SEE ALSO
            help
        """;

    // ?? Script commands ????????????????????????????????????????????

    public const string Ls = """
        LS(1)                     NetNIX Manual                     LS(1)

        NAME
            ls - list directory contents

        SYNOPSIS
            ls [-l] [-a] [dir]

        DESCRIPTION
            List files and directories. Directories shown in blue.

        OPTIONS
            -l          Long format (permissions, owner, size)
            -a          Show hidden files (starting with .)
            -la / -al   Both long format and hidden files

        EXAMPLES
            ls
            ls -la /bin
            ls /home

        SEE ALSO
            tree, find, stat
        """;

    public const string Cat = """
        CAT(1)                    NetNIX Manual                    CAT(1)

        NAME
            cat - display file contents

        SYNOPSIS
            cat [-n] <file> [file2...]

        OPTIONS
            -n          Number output lines

        EXAMPLES
            cat /etc/motd
            cat -n /bin/ls.cs

        SEE ALSO
            head, tail, edit
        """;

    public const string Cp = """
        CP(1)                     NetNIX Manual                     CP(1)

        NAME
            cp - copy files and directories

        SYNOPSIS
            cp [-r] <source> <dest>

        OPTIONS
            -r, -R      Copy directories recursively

        EXAMPLES
            cp file.txt backup.txt
            cp -r /home/alice /home/alice.bak

        SEE ALSO
            mv, rm
        """;

    public const string Mv = """
        MV(1)                     NetNIX Manual                     MV(1)

        NAME
            mv - move or rename files

        SYNOPSIS
            mv <source> <dest>

        EXAMPLES
            mv old.txt new.txt
            mv script.cs /bin/

        SEE ALSO
            cp, rm
        """;

    public const string Rm = """
        RM(1)                     NetNIX Manual                     RM(1)

        NAME
            rm - remove files and directories

        SYNOPSIS
            rm [-r] [-f] <path> [path2...]

        OPTIONS
            -r          Remove directories recursively
            -f          Force (no error if file missing)
            -rf / -fr   Both

        EXAMPLES
            rm file.txt
            rm -rf /tmp/old

        SEE ALSO
            rmdir, cp, mv
        """;

    public const string Mkdir = """
        MKDIR(1)                  NetNIX Manual                  MKDIR(1)

        NAME
            mkdir - create directories

        SYNOPSIS
            mkdir [-p] <dir> [dir2...]

        OPTIONS
            -p          Create parent directories as needed

        EXAMPLES
            mkdir myproject
            mkdir -p /home/user/src/project/lib

        SEE ALSO
            rmdir, ls
        """;

    public const string Rmdir = """
        RMDIR(1)                  NetNIX Manual                  RMDIR(1)

        NAME
            rmdir - remove empty directories

        SYNOPSIS
            rmdir <dir> [dir2...]

        DESCRIPTION
            Remove directories only if they are empty. Use rm -r to
            remove non-empty directories.

        SEE ALSO
            mkdir, rm
        """;

    public const string Touch = """
        TOUCH(1)                  NetNIX Manual                  TOUCH(1)

        NAME
            touch - create empty files

        SYNOPSIS
            touch <file> [file2...]

        DESCRIPTION
            Create an empty file if it doesn't exist. Does nothing
            if the file already exists.

        SEE ALSO
            edit, write
        """;

    public const string Head = """
        HEAD(1)                   NetNIX Manual                   HEAD(1)

        NAME
            head - show first lines of a file

        SYNOPSIS
            head [-n N] <file> [file2...]

        OPTIONS
            -n N        Show first N lines (default 10)
            -N          Short form (e.g. -5 for first 5 lines)

        EXAMPLES
            head /var/log/syslog
            head -n 3 /etc/motd

        SEE ALSO
            tail, cat
        """;

    public const string Tail = """
        TAIL(1)                   NetNIX Manual                   TAIL(1)

        NAME
            tail - show last lines of a file

        SYNOPSIS
            tail [-n N] <file> [file2...]

        OPTIONS
            -n N        Show last N lines (default 10)
            -N          Short form (e.g. -20 for last 20 lines)

        SEE ALSO
            head, cat
        """;

    public const string Wc = """
        WC(1)                     NetNIX Manual                     WC(1)

        NAME
            wc - count lines, words, and bytes

        SYNOPSIS
            wc [-l] [-w] [-c] <file> [file2...]

        OPTIONS
            -l          Count lines only
            -w          Count words only
            -c          Count bytes only
            (none)      Show all three counts

        EXAMPLES
            wc /bin/ls.cs
            wc -l /etc/passwd

        SEE ALSO
            cat, grep
        """;

    public const string Grep = """
        GREP(1)                   NetNIX Manual                   GREP(1)

        NAME
            grep - search files for a pattern

        SYNOPSIS
            grep [-i] [-n] [-v] [-c] <pattern> <file> [file2...]

        OPTIONS
            -i          Case-insensitive match
            -n          Show line numbers
            -v          Invert match (show non-matching lines)
            -c          Count matches only

        EXAMPLES
            grep "Console" /bin/ls.cs
            grep -in "error" /var/log/*.txt
            grep -c "using" /bin/cat.cs

        SEE ALSO
            find, cat
        """;

    public const string Find = """
        FIND(1)                   NetNIX Manual                   FIND(1)

        NAME
            find - search for files in the filesystem

        SYNOPSIS
            find [dir] [-name <pattern>] [-type f|d]

        OPTIONS
            -name <pat>     Filter by name (* wildcards)
            -type f         Files only
            -type d         Directories only

        PATTERNS
            *.cs            Files ending in .cs
            *test*          Files containing "test"
            readme          Exact name match

        EXAMPLES
            find /bin -name *.cs
            find / -type d
            find /home -name *.txt -type f

        SEE ALSO
            ls, grep, tree
        """;

    public const string Tee = """
        TEE(1)                    NetNIX Manual                    TEE(1)

        NAME
            tee - write input to file and stdout

        SYNOPSIS
            tee [-a] <file>

        OPTIONS
            -a          Append instead of overwrite

        DESCRIPTION
            Read text interactively and write it both to the screen
            and to a file. Enter '.' on a line to finish.

        SEE ALSO
            echo, write
        """;

    public const string Echo = """
        ECHO(1)                   NetNIX Manual                   ECHO(1)

        NAME
            echo - print text

        SYNOPSIS
            echo [-n] <text...>

        OPTIONS
            -n          Do not print trailing newline

        DESCRIPTION
            Print arguments to stdout. Combine with > or >> to
            write to files.

        EXAMPLES
            echo Hello World
            echo -n "no newline"
            echo "line 1" > file.txt
            echo "line 2" >> file.txt

        SEE ALSO
            cat, write, tee
        """;

    public const string Pwd = """
        PWD(1)                    NetNIX Manual                    PWD(1)

        NAME
            pwd - print working directory

        SYNOPSIS
            pwd

        SEE ALSO
            cd, ls
        """;

    public const string Whoami = """
        WHOAMI(1)                 NetNIX Manual                 WHOAMI(1)

        NAME
            whoami - print current username

        SYNOPSIS
            whoami

        SEE ALSO
            id, users, su
        """;

    public const string Id = """
        ID(1)                     NetNIX Manual                     ID(1)

        NAME
            id - print user and group IDs

        SYNOPSIS
            id [username]

        EXAMPLES
            id
            id alice

        SEE ALSO
            whoami, users, groups
        """;

    public const string Uname = """
        UNAME(1)                  NetNIX Manual                  UNAME(1)

        NAME
            uname - print system information

        SYNOPSIS
            uname [-a] [-s] [-r] [-n]

        OPTIONS
            -s          System name
            -r          Release version
            -n          Hostname
            -a          All information

        SEE ALSO
            hostname, env
        """;

    public const string Hostname = """
        HOSTNAME(1)               NetNIX Manual               HOSTNAME(1)

        NAME
            hostname - print system hostname

        SYNOPSIS
            hostname

        SEE ALSO
            uname, env
        """;

    public const string Date = """
        DATE(1)                   NetNIX Manual                   DATE(1)

        NAME
            date - print current date and time

        SYNOPSIS
            date [-u] [-I] [+%s]

        OPTIONS
            -u          UTC time
            -I          ISO 8601 date format (yyyy-MM-dd)
            +%s         Unix timestamp (seconds since epoch)

        SEE ALSO
            env, uname
        """;

    public const string Env = """
        ENV(1)                    NetNIX Manual                    ENV(1)

        NAME
            env - display environment variables

        SYNOPSIS
            env

        DESCRIPTION
            Shows current environment: USER, UID, GID, HOME, PWD,
            SHELL, HOSTNAME, PATH, TERM.

        SEE ALSO
            whoami, uname, hostname
        """;

    public const string Basename = """
        BASENAME(1)               NetNIX Manual               BASENAME(1)

        NAME
            basename - strip directory from path

        SYNOPSIS
            basename <path> [suffix]

        EXAMPLES
            basename /bin/ls.cs          ? ls.cs
            basename /bin/ls.cs .cs      ? ls

        SEE ALSO
            dirname
        """;

    public const string Dirname = """
        DIRNAME(1)                NetNIX Manual                DIRNAME(1)

        NAME
            dirname - strip filename from path

        SYNOPSIS
            dirname <path>

        EXAMPLES
            dirname /bin/ls.cs           ? /bin

        SEE ALSO
            basename
        """;

    public const string Du = """
        DU(1)                     NetNIX Manual                     DU(1)

        NAME
            du - estimate disk usage

        SYNOPSIS
            du [-s] [-h] [dir]

        OPTIONS
            -s          Summary (total only)
            -h          Human-readable sizes (K, M)

        EXAMPLES
            du /home
            du -sh /bin

        SEE ALSO
            df, ls
        """;

    public const string Df = """
        DF(1)                     NetNIX Manual                     DF(1)

        NAME
            df - show filesystem summary

        SYNOPSIS
            df

        DESCRIPTION
            Show total size, file count, and directory count for
            the virtual filesystem.

        SEE ALSO
            du, ls
        """;

    public const string Yes = """
        YES(1)                    NetNIX Manual                    YES(1)

        NAME
            yes - output a string repeatedly

        SYNOPSIS
            yes [text]

        DESCRIPTION
            Print "y" (or specified text) 100 times. Useful for
            testing output redirection.

        EXAMPLES
            yes
            yes hello > /dev/null

        SEE ALSO
            echo
        """;

    public const string TrueFalse = """
        TRUE(1) / FALSE(1)        NetNIX Manual         TRUE(1) / FALSE(1)

        NAME
            true - return success (exit code 0)
            false - return failure (exit code 1)

        SYNOPSIS
            true
            false

        SEE ALSO
            echo, yes
        """;

    public const string Curl = """
        CURL(1)                   NetNIX Manual                   CURL(1)

        NAME
            curl - transfer data from a URL

        SYNOPSIS
            curl [options] <url>

        DESCRIPTION
            Perform HTTP requests from the NetNIX shell. Supports GET,
            POST, PUT, DELETE with custom headers and body.

        OPTIONS
            -X METHOD            HTTP method (GET, POST, PUT, DELETE)
            -d DATA              Request body (implies POST if no -X)
            -H "Header: Value"   Add a request header
            --content-type TYPE  Content type (default: application/json)
            -o FILE              Save response body to a VFS file
            -i                   Include response headers in output
            -I                   HEAD request only (show status code)
            -s                   Silent mode (suppress error messages)

        EXAMPLES
            curl https://example.com
            curl -X POST -d '{"key":"val"}' https://httpbin.org/post
            curl -H "Accept: text/plain" https://httpbin.org/get
            curl -o page.html https://example.com
            curl -I https://example.com

        SEE ALSO
            fetch, wget, man api
        """;

    public const string Wget = """
        WGET(1)                   NetNIX Manual                   WGET(1)

        NAME
            wget - download a file from a URL into the VFS

        SYNOPSIS
            wget <url> [output-file]

        DESCRIPTION
            Download a file from the internet and save it to the virtual
            filesystem. If no output filename is given, it is derived
            from the URL.

        EXAMPLES
            wget https://example.com/file.txt
            wget https://example.com/data.json mydata.json
            wget https://example.com/ index.html

        SEE ALSO
            curl, fetch, man api
        """;

    public const string Fetch = """
        FETCH(1)                  NetNIX Manual                  FETCH(1)

        NAME
            fetch - quick HTTP GET and print response

        SYNOPSIS
            fetch <url>

        DESCRIPTION
            Perform a simple HTTP GET request and print the response
            body to stdout. This is the simplest way to retrieve
            web content from a script or the shell.

        EXAMPLES
            fetch https://httpbin.org/get
            fetch https://api.github.com
            fetch https://example.com > page.html

        SEE ALSO
            curl, wget, man api
        """;

    public const string Netlib = """
        NETLIB(3)                 NetNIX Manual                 NETLIB(3)

        NAME
            netlib - networking utility library for scripts

        SYNOPSIS
            #include <netlib>

        DESCRIPTION
            A library providing helper methods for common HTTP patterns.

        METHODS
            NetLib.GetOrDefault(api, url, fallback)
                Fetch URL, return fallback on failure.

            NetLib.Ping(api, url)
                Check if URL is reachable (returns bool).

            NetLib.FetchAll(api, url1, url2, ...)
                Fetch multiple URLs, returns (url, body)[] array.

            NetLib.PostJson(api, url, json)
                POST JSON, returns response body.

            NetLib.DownloadWithStatus(api, url, path)
                Download to VFS, returns status message string.

            NetLib.JsonValue(json, key)
                Extract a value from JSON by key name.
                Basic pattern match (not a full parser).

            NetLib.FormatHeaders(response)
                Format NixHttpResponse headers as string.

        EXAMPLE
            #include <netlib>

            public static class MyCmd
            {
                public static int Run(NixApi api, string[] args)
                {
                    string body = NetLib.GetOrDefault(api,
                        "https://httpbin.org/get", "{}");
                    string origin = NetLib.JsonValue(body, "origin");
                    Console.WriteLine($"Your IP: {origin}");
                    return 0;
                }
            }

        SEE ALSO
            man include, man api, curl, fetch, wget
        """;

    public const string Zip = """
        ZIP(1)                    NetNIX Manual                    ZIP(1)

        NAME
            zip - create and manage zip archives

        SYNOPSIS
            zip <archive.zip> <file|dir> [file|dir ...]
            zip -a <archive.zip> <file> [...]
            zip -l <archive.zip>

        DESCRIPTION
            Create zip archives from files and directories in the VFS.
            Archives are stored as binary files and can be extracted
            with the unzip command.

        OPTIONS
            zip <archive> <sources...>   Create new archive
            zip -a <archive> <files...>  Add files to existing archive
            zip -l <archive>             List archive contents

        EXAMPLES
            zip backup.zip file1.txt file2.txt
            zip project.zip /home/user/src
            zip -a backup.zip newfile.txt
            zip -l backup.zip

        SEE ALSO
            unzip, man ziplib, man api
        """;

    public const string Unzip = """
        UNZIP(1)                  NetNIX Manual                  UNZIP(1)

        NAME
            unzip - extract files from a zip archive

        SYNOPSIS
            unzip <archive.zip> [dest-dir]
            unzip -l <archive.zip>

        DESCRIPTION
            Extract files from a zip archive into the virtual filesystem.
            If no destination directory is given, files are extracted to
            the current directory.

        OPTIONS
            unzip <archive> [dir]   Extract to directory (default: cwd)
            unzip -l <archive>      List archive contents

        EXAMPLES
            unzip backup.zip
            unzip backup.zip /tmp/extracted
            unzip -l backup.zip

        SEE ALSO
            zip, man ziplib, man api
        """;

    public const string Ziplib = """
        ZIPLIB(3)                 NetNIX Manual                 ZIPLIB(3)

        NAME
            ziplib - zip archive utility library for scripts

        SYNOPSIS
            #include <ziplib>

        DESCRIPTION
            A library providing helper methods for working with zip
            archives in the VFS from scripts.

        METHODS
            ZipLib.ZipDirectory(api, dir, zipPath)
                Create a zip from an entire directory.

            ZipLib.ZipByExtension(api, dir, ext, zipPath)
                Zip all files in dir matching extension (e.g. ".cs").

            ZipLib.ExtractTo(api, zipPath, destDir)
                Extract archive to directory. Returns file count.

            ZipLib.ListEntries(api, zipPath)
                Get filenames inside a zip (string[]).

            ZipLib.TotalSize(api, zipPath)
                Total uncompressed size of all entries.

            ZipLib.CompressionRatio(api, zipPath)
                Compression ratio (0.0 to 1.0).

            ZipLib.Contains(api, zipPath, entryName)
                Check if zip contains a specific file.

            ZipLib.BackupDirectory(api, dir, backupDir)
                Create timestamped backup zip. Returns zip path.

        EXAMPLE
            #include <ziplib>

            public static class BackupCmd
            {
                public static int Run(NixApi api, string[] args)
                {
                    string zip = ZipLib.BackupDirectory(api,
                        "/home/user", "/tmp/backups");
                    if (zip != null)
                        Console.WriteLine($"Backup: {zip}");
                    return zip != null ? 0 : 1;
                }
            }

        SEE ALSO
            zip, unzip, man include, man api
        """;

    public const string Cbpaste = """
        CBPASTE(1)                NetNIX Manual                CBPASTE(1)

        NAME
            cbpaste - paste host clipboard into the NetNIX environment

        SYNOPSIS
            cbpaste [file]
            cbpaste -a <file>
            cbpaste -p

        DESCRIPTION
            Reads the host system's clipboard and either prints it to
            stdout or writes it to a file in the virtual filesystem.

            This bridges the host OS clipboard with the NetNIX VFS,
            allowing you to paste code, text, or data from your host
            into the environment.

        OPTIONS
            cbpaste             Print clipboard to stdout
            cbpaste <file>      Write clipboard to file (creates/overwrites)
            cbpaste -a <file>   Append clipboard to file
            cbpaste -p          Print clipboard to stdout (explicit)

        EXAMPLES
            cbpaste                     Print clipboard contents
            cbpaste notes.txt           Save clipboard to notes.txt
            cbpaste -a log.txt          Append clipboard to log.txt
            cbpaste mycode.cs           Paste code from host into a script
            cbpaste /bin/newcmd.cs      Install a command from clipboard

        PLATFORM SUPPORT
            Windows     Uses powershell Get-Clipboard
            macOS       Uses pbpaste
            Linux       Uses xclip or xsel

        SEE ALSO
            cbcopy, echo, write, edit, man api
        """;

    public const string Cbcopy = """
        CBCOPY(1)                 NetNIX Manual                 CBCOPY(1)

        NAME
            cbcopy - copy a VFS file to the host clipboard

        SYNOPSIS
            cbcopy <file>

        DESCRIPTION
            Reads a text file from the virtual filesystem and copies
            its contents to the host system's clipboard.

            This bridges the NetNIX VFS with the host OS clipboard,
            allowing you to export code, text, or data from the
            environment to your host applications.

        EXAMPLES
            cbcopy notes.txt           Copy notes to host clipboard
            cbcopy /bin/ls.cs          Copy a script source to clipboard
            cbcopy ~/.nshrc            Copy startup script to clipboard
            cbcopy /etc/motd           Copy message of the day

        PLATFORM SUPPORT
            Windows     Uses clip.exe
            macOS       Uses pbcopy
            Linux       Uses xclip or xsel

        SEE ALSO
            cbpaste, cat, man api
        """;

    public const string Include = """
        INCLUDE(7)                NetNIX Manual                INCLUDE(7)

        NAME
            include - C# script library include system

        DESCRIPTION
            The #include directive lets C# scripts (.cs) import shared
            library code before compilation. Libraries are plain .cs
            files stored in /lib (or /usr/lib, /usr/local/lib).

        SYNTAX
            #include <libname>          Search /lib for libname.cs
            #include "path/to/file.cs"  Include by relative/absolute path

            The directive must be on its own line. It is processed before
            compilation — the library source is merged into the script.

        LIBRARY SEARCH ORDER
            1. /lib/<name>.cs
            2. /usr/lib/<name>.cs
            3. /usr/local/lib/<name>.cs

        CREATING A LIBRARY
            Libraries are normal .cs files without a Run() method.
            They typically contain utility classes and static methods:

                edit /lib/myutils.cs

            Example library:
                public static class MyUtils
                {
                    public static void Greet(string name)
                    {
                        Console.WriteLine($"Hello, {name}!");
                    }
                }

        USING A LIBRARY
            In any .cs script, add an #include at the top:

                using System;
                using NetNIX.Scripting;

                #include <myutils>

                public static class MyCommand
                {
                    public static int Run(NixApi api, string[] args)
                    {
                        MyUtils.Greet(api.Username);
                        return 0;
                    }
                }

        FEATURES
            - Recursive includes (libraries can include other libraries)
            - Circular include detection (automatically skipped)
            - Cached compilation (same combined source ? same assembly)

        BUILT-IN LIBRARIES
            demoapilib      Formatted output, file utils, system info

        DEMO
            Run 'demoapitest' to see #include in action.

        SEE ALSO
            man api, man scripting, run, edit
        """;

    public const string Demoapilib = """
        DEMOAPILIB(3)             NetNIX Manual             DEMOAPILIB(3)

        NAME
            demoapilib - demonstration API library

        SYNOPSIS
            #include <demoapilib>

        DESCRIPTION
            A built-in library providing helper methods for formatted
            output, file utilities, and system information.

        CLASSES
            DemoApiLib              All methods are static

        FORMATTED OUTPUT
            DemoApiLib.PrintHeader(title)     Colored section header
            DemoApiLib.PrintValue(label, val) Labeled value display
            DemoApiLib.PrintOk(message)       Green success message
            DemoApiLib.PrintError(message)    Red error message
            DemoApiLib.PrintInfo(message)     Cyan info message
            DemoApiLib.Separator()            Blank line

        FILE UTILITIES
            DemoApiLib.CountLines(api, path)              Line count
            DemoApiLib.GetExtension(path)                 File extension
            DemoApiLib.IsCsFile(path)                     Is .cs file?
            DemoApiLib.IsShFile(path)                     Is .sh file?
            DemoApiLib.FindByExtension(api, dir, ext)     Find files

        SYSTEM INFO
            DemoApiLib.GetSystemSummary(api)              Env summary
            DemoApiLib.DirectorySize(api, dir)            Total dir size

        STRING UTILITIES
            DemoApiLib.Pad(text, width)                   Pad/truncate
            DemoApiLib.TableRow((text,width), ...)        Table row

        EXAMPLE
            #include <demoapilib>

            public static class MyCmd
            {
                public static int Run(NixApi api, string[] args)
                {
                    DemoApiLib.PrintHeader("My Tool");
                    DemoApiLib.PrintValue("User", api.Username);
                    DemoApiLib.PrintOk("Done!");
                    return 0;
                }
            }

        SEE ALSO
            man include, demoapitest, man api
        """;

    public const string Demoapitest = """
        DEMOAPITEST(1)            NetNIX Manual            DEMOAPITEST(1)

        NAME
            demoapitest - demonstrate the #include library system

        SYNOPSIS
            demoapitest

        DESCRIPTION
            A demo command that uses #include <demoapilib> to show how
            scripts can import and use shared library code.

            The demo exercises every method in DemoApiLib:
            formatted output, file scanning, directory sizing,
            system info, and table formatting.

        SEE ALSO
            man include, man demoapilib, man scripting
        """;

    public const string Source = """
        SOURCE(1)                 NetNIX Manual                 SOURCE(1)

        NAME
            source - execute a shell script

        SYNOPSIS
            source <file>
            . <file>

        DESCRIPTION
            Read and execute commands from a file in the current shell.
            Each line is executed as if typed at the prompt. Blank lines
            and lines starting with # are skipped.

            The '.' command is an alias for source.

        VARIABLE EXPANSION
            The following variables are expanded before execution:

            $USER       Current username
            $UID        Current user ID
            $GID        Current group ID
            $HOME       Home directory
            $CWD        Current working directory
            $PWD        Current working directory (alias)
            $SHELL      Shell path (/bin/nsh)
            $HOSTNAME   System hostname (netnix)
            ~           Expands to home directory

        EXAMPLES
            source ~/.nshrc
            . /etc/profile
            source setup.sh

        SEE ALSO
            man nshrc, help
        """;

    public const string Nshrc = """
        NSHRC(5)                  NetNIX Manual                  NSHRC(5)

        NAME
            nshrc - shell startup script

        DESCRIPTION
            When the nsh shell starts, it automatically sources the
            file ~/.nshrc if it exists. This allows users to customize
            their login experience with welcome messages, aliases, or
            any shell commands.

        FILE FORMAT
            Plain text, one command per line:

            # This is a comment
            echo "Welcome back, $USER!"
            date
            cat /etc/motd

            Blank lines and lines starting with # are ignored.
            Shell variables ($USER, $UID, $HOME, etc.) are expanded.

        FILES
            ~/.nshrc            Per-user startup script
            /etc/skel/.nshrc    Default template for new users

        DEFAULT CONTENTS
            The default .nshrc installed for each new user:

            # Welcome message
            echo "Welcome back, $USER (uid=$UID)!"
            cat /etc/motd
            date
            echo "Type 'help' for commands."

        CUSTOMIZATION
            Edit your startup script:
                edit ~/.nshrc

            Example additions:
                # Show directory listing on login
                ls

                # Show disk usage summary
                df

                # Custom greeting
                echo "Good day, $USER. You are in $CWD."

        CREATING SHELL SCRIPTS
            Any text file can be used as a shell script:

                edit myscript.sh
                # Add commands, one per line
                source myscript.sh

        SEE ALSO
            source, help, man api, man scripting
        """;

    public const string Export = """
        EXPORT(8)                 NetNIX Manual                 EXPORT(8)

        NAME
            export - export the virtual filesystem to a host zip archive

        SYNOPSIS
            export [options] <host-path> [vfs-root]

        DESCRIPTION
            Exports the contents of the NetNIX virtual filesystem (or a
            subtree) to a standard zip archive on the host operating system.

            By default, mounted filesystems (e.g. /mnt/*) are excluded
            from the export. Use --mounts to include them.

            The exported zip contains the raw files and directories without
            VFS metadata (permissions, ownership). It is a plain zip that
            can be opened with any archive tool on the host.

            Only root (uid 0) can export the filesystem.

        ARGUMENTS
            <host-path>     Full path on the host OS for the output zip
            [vfs-root]      VFS path to export from (default: / for all)

        OPTIONS
            -m, --mounts    Include mounted filesystems in the export
            -h, --help      Show help

        EXAMPLES
            export C:\Backups\netnix-full.zip
            export C:\Backups\netnix-full.zip /
            export --mounts C:\Backups\everything.zip
            export C:\Backups\home-alice.zip /home/alice
            export C:\Backups\etc-backup.zip /etc

        NOTES
            VFS metadata (permissions, owners, groups) is not included.
            Mounted filesystems are excluded by default.
            To back up the full rootfs with metadata, copy the rootfs.zip
            file directly from the host AppData directory.

        SEE ALSO
            mount, umount, zip, unzip
        """;

    public const string Mount = """
        MOUNT(8)                  NetNIX Manual                  MOUNT(8)

        NAME
            mount - mount a host zip archive into the virtual filesystem

        SYNOPSIS
            mount [options] <host-path> <mount-point>
            mount --sync <mount-point>
            mount

        DESCRIPTION
            Mounts an external zip archive from the host operating system
            into the NetNIX virtual filesystem at the specified mount
            point. The contents of the zip become accessible as regular
            files and directories under the mount point.

            By default, mounts are read-only (ro): changes are held in
            memory and not written back to the host zip unless you use
            --sync or umount --save.

            With --rw (writable mode), every mutation (file create, write,
            delete, move, copy) under the mount point is automatically
            saved back to the host zip as it happens.

            With no arguments, lists all active mounts and their mode.

            Only root (uid 0) can mount, unmount, and sync archives.

        OPTIONS
            -w, --rw        Writable mode: auto-save changes to host zip
            -s, --sync      Save in-memory changes back to the host zip
            -h, --help      Show help

        EXAMPLES
            mount extra.zip /mnt/extra                     (read-only)
            mount --rw extra.zip /mnt/extra                (writable, auto-save)

            ls /mnt/extra
            echo "hello" > /mnt/extra/notes.txt
            mount --sync /mnt/extra                        (manual save)
            umount --save /mnt/extra                       (save + unmount)

            mount                  (list active mounts with ro/rw mode)

        NOTES
            The mount point directory is created automatically.
            In rw mode the host zip is rewritten on every change.
            In ro mode the host zip is untouched unless you sync.

        SEE ALSO
            umount, zip, unzip
        """;

    public const string Umount = """
        UMOUNT(8)                 NetNIX Manual                 UMOUNT(8)

        NAME
            umount - unmount a previously mounted archive

        SYNOPSIS
            umount [options] <mount-point>

        DESCRIPTION
            Removes a previously mounted archive from the virtual
            filesystem. All files and directories under the mount point
            are removed from memory.

            With the --save flag, changes made to files under the mount
            point are written back to the original host zip archive
            before unmounting.

            Without --save, all in-memory changes are discarded.

            Only root (uid 0) can unmount archives.

        OPTIONS
            -s, --save      Save changes to the host zip before unmounting

        EXAMPLES
            umount /mnt/data              Discard changes and unmount
            umount --save /mnt/data       Save changes, then unmount

        SEE ALSO
            mount, zip, unzip
        """;

    public const string ApiReference = """
        API(7)                    NetNIX Manual                    API(7)

        NAME
            api - NixApi scripting reference

        DESCRIPTION
            Scripts receive a NixApi instance as their first parameter.
            This is the complete API surface available to scripts.

        PROPERTIES
            api.Uid                 Current user's UID
            api.Gid                 Current user's GID
            api.Username            Current username
            api.Cwd                 Current working directory

        PATH HELPERS
            api.ResolvePath(path)             Resolve relative to cwd
            api.GetName(path)                 Filename from path
            api.GetParent(path)               Parent directory

        FILE SYSTEM QUERIES
            api.Exists(path)                  File or directory exists?
            api.IsDirectory(path)             Is a directory?
            api.IsFile(path)                  Is a file?
            api.ListDirectory(path)           List entries (string[])
            api.GetPermissions(path)          Permission string (rwx...)
            api.GetPermissionString(path)     With type prefix (drwx...)
            api.GetOwner(path)                Owner UID
            api.GetGroup(path)                Group GID
            api.GetSize(path)                 File size in bytes
            api.CanRead(path)                 Readable by current user?
            api.CanWrite(path)                Writable by current user?
            api.CanExecute(path)              Executable by current user?

        ABSOLUTE PATH QUERIES (for raw VFS paths)
            api.IsDir(vfsPath)                Is directory? (absolute)
            api.NodeName(vfsPath)             Name from absolute path
            api.ExistsAbsolute(vfsPath)       Exists? (absolute)
            api.IsDirAbsolute(vfsPath)        Is directory? (absolute)
            api.GetSizeAbsolute(vfsPath)      Size (absolute)
            api.GetAllPaths()                 All VFS paths (string[])

        FILE I/O
            api.ReadText(path)                Read file as string
            api.ReadBytes(path)               Read file as byte[]
            api.WriteText(path, content)      Write string to file
            api.WriteBytes(path, data)        Write byte[] to file
            api.AppendText(path, content)     Append string to file
            api.CreateEmptyFile(path)         Create empty file
            api.CreateDir(path)               Create directory
            api.CreateDirWithParents(path)    Create with parents (mkdir -p)
            api.Delete(path)                  Delete file or directory
            api.Copy(src, dest)               Copy file/directory
            api.Move(src, dest)               Move/rename

        USER QUERIES
            api.GetUsername(uid)              Username from UID
            api.GetGroupName(gid)            Group name from GID
            api.GetAllUsers()                All users (tuple[])
            api.GetAllGroups()               All groups (tuple[])
            api.UserCount                    Number of users
            api.GroupCount                   Number of groups
            api.GetUserGroups(username)      Supplementary groups (string[])
            api.IsUserLocked(username)       Check if account is locked

        USER MANAGEMENT (root only)
            api.CreateUser(name, pass, home?)    Create user account
            api.DeleteUser(name, removeHome?)    Delete user account
            api.ChangePassword(name, pass)       Change password
            api.ModifyUser(name, home?, shell?)  Change home/shell
            api.LockUser(name)                   Lock account
            api.UnlockUser(name)                 Unlock account

        GROUP MANAGEMENT (root only)
            api.CreateGroup(name)                Create group
            api.DeleteGroup(name)                Delete group
            api.RenameGroup(old, new)            Rename group
            api.AddUserToGroup(user, group)      Add user to group
            api.RemoveUserFromGroup(user, group) Remove user from group

        HOST CLIPBOARD
            api.GetClipboard()               Read host clipboard as string
                                             Returns null if empty/unavailable
            api.SetClipboard(text)           Write text to host clipboard
                                             Returns true on success
            api.FileToClipboard(path)        Copy VFS file to host clipboard
                                             Returns true on success
            api.ClipboardToFile(path)        Write clipboard to a VFS file
                                             Returns true on success
            api.ClipboardAppendToFile(path)  Append clipboard to a VFS file
                                             Returns true on success

            Clipboard access is platform-aware:
              Windows   clip.exe / powershell Get-Clipboard
              macOS     pbcopy / pbpaste
              Linux     xclip / xsel

        NETWORKING
            api.Net                          NixNet networking object
            api.Download(url, path)          Download URL to VFS file (bytes)
            api.DownloadText(url, path)      Download URL to VFS file (text)

            api.Net.Get(url)                 HTTP GET, returns string
            api.Net.GetBytes(url)            HTTP GET, returns byte[]
            api.Net.Post(url, body, type?)   HTTP POST, returns string
            api.Net.PostForm(url, fields)    POST form data
            api.Net.Put(url, body, type?)    HTTP PUT, returns string
            api.Net.Delete(url)              HTTP DELETE, returns string
            api.Net.Head(url)                HTTP HEAD, returns status code
            api.Net.IsReachable(url)         Check if URL returns 2xx
            api.Net.Request(method, url,     Full control: custom method,
              body?, type?, headers...)       headers, returns NixHttpResponse

            NixHttpResponse properties:
              .StatusCode    int       HTTP status code
              .StatusText    string    Reason phrase
              .IsSuccess     bool      True if 2xx
              .Body          string    Response body
              .Headers       tuple[]   Response headers
              .GetHeader(n)  string?   Get header by name

        ZIP ARCHIVES
            api.ZipCreate(zipPath, paths...)  Create zip from files/dirs
            api.ZipExtract(zipPath, destDir)  Extract zip into directory
                                              Returns file count or -1
            api.ZipList(zipPath)              List entries as tuple[]
                                              (name, compressed, uncompressed)
            api.ZipAddFile(zipPath, file,     Add file to existing/new zip
              entryName?)                     Optional name inside archive

        PERSISTENCE
            api.Save()                       Save filesystem to disk

        EXAMPLE
            using System;
            using NetNIX.Scripting;

            public static class MyCommand
            {
                public static int Run(NixApi api, string[] args)
                {
                    foreach (var path in api.ListDirectory("."))
                    {
                        string name = api.NodeName(path);
                        int size = api.GetSizeAbsolute(path);
                        Console.WriteLine($"{name}: {size} bytes");
                    }
                    return 0;
                }
            }

        SEE ALSO
            man scripting, run, edit
        """;

    public const string ScriptingGuide = """
        SCRIPTING(7)              NetNIX Manual              SCRIPTING(7)

        NAME
            scripting - how to write NetNIX C# scripts

        OVERVIEW
            NetNIX scripts are plain-text .cs files compiled at runtime
            using Roslyn. They are stored in the virtual filesystem and
            can be edited with the 'edit' command.

        SCRIPT CONTRACT
            Every script must contain a class with this method:

                public static int Run(NixApi api, string[] args)

            - The class name does not matter.
            - Return 0 for success, non-zero for failure.
            - The NixApi object gives access to the filesystem, user
              info, and other environment data.

        CREATING A SCRIPT
            1. Create the file:
                   edit hello.cs

            2. Press Ctrl+T ? 1 to insert a script template

            3. Edit the code, F2 to save, Ctrl+Q to quit

            4. Run it:
                   hello               (from same directory)
                   run hello.cs        (explicit)
                   run /path/hello.cs  (absolute)

        INSTALLING AS A COMMAND
            Place the script in /bin/ to make it available everywhere:

                cp hello.cs /bin/hello.cs

            Now 'hello' works from any directory.

        COMMAND RESOLUTION ORDER
            1. Shell builtins (cd, edit, help, su, etc.)
            2. .cs files in the current working directory
            3. .cs files in /bin, /usr/bin, /usr/local/bin

        AVAILABLE IMPORTS
            Scripts can use any .NET 8 BCL type, plus:
                using NetNIX.Scripting;    (for NixApi)

        COMPILATION ERRORS
            If your script has errors, they are printed to the console
            with line numbers. Fix them with 'edit' and try again.

        CACHING
            Compiled scripts are cached in memory by source hash.
            Editing the file invalidates the cache automatically.

        ADDING HELP FOR YOUR SCRIPT
            Create a man page at:
                edit /usr/share/man/mycommand.txt

            Then users can type: man mycommand

        SEE ALSO
            man api, edit, run
        """;

    public const string EditorGuide = """
        EDITOR(7)                 NetNIX Manual                 EDITOR(7)

        NAME
            editor - nedit text editor guide

        OVERVIEW
            nedit is a nano-style full-screen editor built into NetNIX.
            Launch it with: edit <filename>

        INTERFACE
            ?? Title bar ??? filename, modified status, cursor position ??
            ?  1 ? line content                                          ?
            ?  2 ? line content                                          ?
            ?  ~ ?                                     (empty lines)     ?
            ?? Status ??? save confirmation, errors ??????????????????????
            ?? Shortcut bar ??? ^S Save  ^Q Quit  ^T Template ???????????

        NAVIGATION
            Arrow keys          Move cursor
            Home / End          Start / end of line
            Page Up / Down      Scroll by screen
            Ctrl+G              Jump to line number

        EDITING
            Type                Insert characters
            Enter               New line (auto-indents)
            Tab                 Insert 4 spaces
            Backspace           Delete backward (merges lines)
            Delete              Delete forward (merges lines)
            Ctrl+K              Cut entire line
            Ctrl+U              Paste cut line

        FILE OPERATIONS
            F2 / Ctrl+W         Save
            Ctrl+Q              Quit (prompts if unsaved)
            F10                 Quit (alternative)

        C# TEMPLATES (Ctrl+T)
            Quick-insert code skeletons:
            1 - Script      Ready-to-run NixApi script
            2 - Class       Class with constructor
            3 - Main        Script with arg parsing
            4 - Snippet     Common API usage examples

        TIPS
            - Auto-indent matches the current line's leading spaces
            - Line numbers are shown in the gutter
            - The title bar shows your cursor position (Ln, Col)
            - Templates suppress auto-indent for clean insertion

        SEE ALSO
            man scripting, man api
        """;

    public const string FilesystemGuide = """
        FILESYSTEM(7)             NetNIX Manual             FILESYSTEM(7)

        NAME
            filesystem - NetNIX filesystem hierarchy

        DESCRIPTION
            NetNIX follows the Filesystem Hierarchy Standard (FHS).
            The entire filesystem is stored in a single .zip archive.

        DIRECTORY STRUCTURE
            /               Root directory
            /bin            Essential command scripts (.cs files)
            /boot           Boot files (reserved)
            /dev            Device files (reserved)
            /etc            System configuration
            /etc/passwd     User account database
            /etc/shadow     Password hashes
            /etc/group      Group database
            /etc/motd       Message of the day
            /etc/skel       Skeleton for new home directories
            /home           User home directories
            /home/<user>    Individual user home
            /lib            Shared libraries (reserved)
            /media          Removable media mount points
            /mnt            Temporary mount points
            /opt            Optional software
            /proc           Process info (reserved, read-only)
            /root           Root user's home directory
            /run            Runtime data
            /sbin           System administration commands
            /srv            Service data
            /sys            System info (reserved, read-only)
            /tmp            Temporary files (world-writable)
            /usr            User programs and data
            /usr/bin        Additional commands
            /usr/local/bin  Locally installed commands
            /usr/share/man  Manual pages (help system)
            /var            Variable data
            /var/log        Log files
            /var/tmp        Persistent temporary files

        FILE STORAGE
            The filesystem is persisted at:
                %APPDATA%/NetNIX/rootfs.zip

        SEE ALSO
            man permissions, ls, tree, df
        """;

    public const string PermissionsGuide = """
        PERMISSIONS(7)            NetNIX Manual            PERMISSIONS(7)

        NAME
            permissions - file permission system

        DESCRIPTION
            NetNIX uses UNIX-style 9-character permission strings.

        FORMAT
            rwxrwxrwx
            \_/\_/\_/
             |  |  ??? Other (everyone else)
             |  ?????? Group
             ????????? Owner

            r = read    w = write    x = execute    - = denied

        OCTAL NOTATION
            r=4  w=2  x=1

            755 = rwxr-xr-x    (owner: all, others: read+exec)
            644 = rw-r--r--    (owner: read+write, others: read)
            700 = rwx------    (owner only)
            777 = rwxrwxrwx    (everyone: all)

        SPECIAL
            Directories need 'x' permission to be entered (cd).
            Scripts need 'r' permission to be executed.
            The sticky bit 't' on /tmp prevents users from deleting
            each other's files.

        ROOT
            Root (uid=0) bypasses all permission checks.

        COMMANDS
            chmod <mode> <path>     Change permissions
            chown <user> <path>     Change owner (root only)
            stat <path>             View current permissions

        EXAMPLES
            chmod 755 /bin/myscript.cs
            chmod rw-r--r-- notes.txt
            stat /etc/passwd

        SEE ALSO
            chmod, chown, stat
        """;

    public const string Sandbox = """
        SANDBOX(5)                NetNIX Manual                SANDBOX(5)

        NAME
            sandbox.conf - script sandbox security configuration

        LOCATION
            /etc/sandbox.conf

        DESCRIPTION
            Controls which .NET namespaces and API patterns are blocked
            in user scripts (.cs files). This is the primary mechanism
            preventing scripts from accessing the host filesystem,
            network, processes, and other dangerous APIs directly.

            The file is readable by root only (permissions rw-r-----).
            Root or sudo users can edit it to add or remove rules.
            Changes take effect on the next script execution.

        FORMAT
            Lines starting with # are comments.  Blank lines are ignored.

            [blocked_usings]
                Namespace prefixes. Any 'using' directive whose namespace
                matches or starts with a listed prefix is blocked.

                Example: "System.IO" blocks:
                    using System.IO;
                    using System.IO.Compression;

            [blocked_tokens]
                Literal strings. If any of these appear anywhere in the
                script source (after #include expansion), the script is
                rejected.

                Example: "File.WriteAll" blocks:
                    File.WriteAllText("...", "...");

        DEFAULT RULES
            Blocked namespaces:
                System.IO, System.Diagnostics, System.Net,
                System.Reflection, System.Runtime.Loader,
                System.Runtime.InteropServices, System.Security,
                System.CodeDom

            Blocked tokens:
                File operations (File.Create, File.Read, File.Write,
                Directory.Create, StreamReader, StreamWriter, etc.),
                Process spawning, network clients, reflection,
                unsafe/interop, environment manipulation.

        EXAMPLES
            To allow scripts to use System.IO.Path (but keep the rest
            of System.IO blocked), you could remove "System.IO" from
            blocked_usings and instead add specific sub-namespaces:

                [blocked_usings]
                System.IO.Compression
                System.IO.Pipes
                # System.IO.Path is now allowed

            To block a custom namespace:

                [blocked_usings]
                MyDangerous.Namespace

            To block a specific method call:

                [blocked_tokens]
                SomeDangerousMethod(

        SEE ALSO
            man scripting, man permissions, man api
        """;
}
