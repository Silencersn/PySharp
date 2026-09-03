using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using System.Security;

namespace PySharp.Console;

internal static class Program
{
    private static PyEnvironmentHost Host { get; } = PyEnvironmentHost.CreateConsole(usingPhysicalFileSystem: true);

    private static int Main(string[] args)
    {
        var builder = Host.CreateEnvironmentBuilder();

        int index = 0;
        int optimizationLevel = 0;
        while (index < args.Length)
        {
            switch (args[index])
            {
                case "-O":
                    optimizationLevel++;
                    index++;
                    continue;
                case "-OO":
                    optimizationLevel += 2;
                    index++;
                    continue;
                case "-S":
                    builder.NotImplyImportSite();
                    index++;
                    continue;
            }
            break;
        }

        if (optimizationLevel is not 0)
            builder.SetOptimizationLevel(optimizationLevel);

        var remaining = args[index..];

        // No arguments (or only global flags): enter the interactive REPL.
        if (remaining.Length is 0)
            return RunRepl(builder);

        // "--" terminates option parsing: everything after it is treated as
        // the script and its arguments (matches CPython).
        if (remaining[0] is "--")
        {
            // "pysharp --" with no script: enter the interactive REPL.
            if (remaining.Length is 1)
                return RunRepl(builder);
            // Even after "--", "-" still selects stdin mode (matches CPython).
            if (remaining[1] is "-")
                return RunStdin(builder, remaining[2..]);
            return RunScript(builder, remaining[1], remaining[2..]);
        }

        switch (remaining[0])
        {
            case "-h":
            case "--help":
                PrintUsage();
                return 0;

            case "-V":
            case "--version":
                PrintVersion();
                return 0;

            case "-c":
                if (remaining.Length < 2)
                {
                    Error("argument -c: expected one argument");
                    return 2;
                }
                // Extra arguments after the code are passed through to sys.argv
                // (matches CPython: sys.argv == ['-c', ...]).
                return RunCode(builder, remaining[1], remaining[2..]);

            default:
                // "-" reads the program from stdin (matches CPython).
                if (remaining[0] is "-")
                    return RunStdin(builder, remaining[1..]);
                if (remaining[0].StartsWith('-'))
                {
                    Error($"unknown option: {remaining[0]}");
                    return 2;
                }
                // Extra arguments after the script are passed through to sys.argv
                // (matches CPython: sys.argv == ['script.py', ...]).
                return RunScript(builder, remaining[0], remaining[1..]);
        }
    }

    private static int RunScript(IPyEnvironmentBuilder builder, string path, string[] scriptArgs)
    {
        if (Directory.Exists(path))
        {
            // Matches CPython: a directory is reported separately with exit code 1.
            Error($"{path} is a directory, cannot continue");
            return 1;
        }

        if (!File.Exists(path))
        {
            Error($"can't open file '{path}': No such file or directory");
            return 2;
        }

        string code;
        try
        {
            code = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or SecurityException or ArgumentException or NotSupportedException)
        {
            // Covers races after File.Exists and any read/permission failure;
            // matches CPython, which reports a clean error and exits with 2.
            if (Directory.Exists(path))
            {
                Error($"{path} is a directory, cannot continue");
                return 1;
            }
            Error($"can't open file '{path}': {DescribeOpenFailure(ex)}");
            return 2;
        }

        var fullPath = Path.GetFullPath(path);
        var scriptDirectory = Path.GetDirectoryName(fullPath)!;

        builder
            .AddPath(scriptDirectory)
            .AddArg(path)
            .AddArgs(scriptArgs);

        using var env = builder.Build();
        using var context = PyCallContext.CreateInterpreterRootContext(env);
        PyInterpreter.PyTryCatch(context, () =>
        {
            PyInterpreter.RunCodeWithContext(context, code,
                Path.GetFileNameWithoutExtension(path),
                fullPath,
                isMain: true);
        });
        return env.ExitCode;
    }

    private static string DescribeOpenFailure(Exception ex) => ex switch
    {
        FileNotFoundException or DirectoryNotFoundException => "No such file or directory",
        UnauthorizedAccessException or SecurityException => "Permission denied",
        PathTooLongException => "File name too long",
        ArgumentException or NotSupportedException => "Invalid argument",
        // CPython's _wopen maps sharing/lock violations to EACCES.
        IOException when IsSharingViolation(ex) => "Permission denied",
        _ => "Input/output error",
    };

    private static bool IsSharingViolation(Exception ex)
    {
        const int ERROR_SHARING_VIOLATION = 0x20;
        const int ERROR_LOCK_VIOLATION = 0x21;
        return (ex.HResult & 0xFFFF) is ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION;
    }

    private static int RunCode(IPyEnvironmentBuilder builder, string code, string[] extraArgs)
    {
        builder
            .AddArg("-c")
            .AddArgs(extraArgs);

        using var env = builder.Build();
        using var context = PyCallContext.CreateInterpreterRootContext(env);
        PyInterpreter.PyTryCatch(context, () =>
        {
            PyInterpreter.RunCodeWithContext(context, code, "<module>", "-c", isMain: true);
        });
        return env.ExitCode;
    }

    private static int RunStdin(IPyEnvironmentBuilder builder, string[] extraArgs)
    {
        builder
            .AddArg("-")
            .AddArgs(extraArgs);

        // Matches CPython: a TTY enters the interactive REPL; redirected
        // stdin is executed as a program ("<stdin>").
        if (!System.Console.IsInputRedirected)
            return RunRepl(builder);

        // CPython sets sys.path[0] = '' (the current directory) for stdin.
        builder.AddPath(Environment.CurrentDirectory);

        using var env = builder.Build();
        var code = env.In.ReadToEnd();
        using var context = PyCallContext.CreateInterpreterRootContext(env);
        PyInterpreter.PyTryCatch(context, () =>
        {
            PyInterpreter.RunCodeWithContext(context, code, "<module>", "<stdin>", isMain: true);
        });
        return env.ExitCode;
    }

    private static int RunRepl(IPyEnvironmentBuilder builder)
    {
        // CPython sets sys.path[0] = '' (the current directory) for the REPL.
        builder.AddPath(Environment.CurrentDirectory);
        builder.SetInteractive(true);
        using var env = builder.Build();
        PyInterpreter.RunRepl(env);
        return 0;
    }

    private static void PrintVersion()
    {
        var version = typeof(PyInterpreter).Assembly.GetName().Version;
        System.Console.WriteLine($"PySharp {version}");
    }

    private static void PrintUsage()
    {
        System.Console.WriteLine(
            """
            Usage: pysharp [options] [script] [arg ...]

            Options:
              -h, --help     show this help message and exit
              -V, --version  print PySharp version and exit
              -O             remove assert and __debug__-dependent statements
              -OO            do -O changes and also discard docstrings
              -S             don't imply 'import site' on initialization
              -c <code>      run the given code as a Python program
              --             stop parsing options; remaining arguments are
                             treated as the script and its arguments
              -              read the program from stdin; if a terminal is
                             attached, enter the interactive REPL

            Arguments after a script or -c <code> are passed through to sys.argv.
            """);
    }

    private static void Error(string message)
    {
        System.Console.Error.WriteLine($"pysharp: {message}");
    }
}
