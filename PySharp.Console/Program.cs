using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;

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
        if (!File.Exists(path))
        {
            Error($"can't open file '{path}': No such file or directory");
            return 2;
        }

        var code = File.ReadAllText(path);
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

    private static int RunRepl(IPyEnvironmentBuilder builder)
    {
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
              -c <code>      run the given code as a Python program
              --             stop parsing options; remaining arguments are
                             treated as the script and its arguments

            Arguments after a script or -c <code> are passed through to sys.argv.
            """);
    }

    private static void Error(string message)
    {
        System.Console.Error.WriteLine($"pysharp: {message}");
    }
}
