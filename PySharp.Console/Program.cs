using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Environments;

namespace PySharp.Console;

internal static class Program
{
    private const string AnsiColorRed = "\e[31m";
    private const string AnsiClearColor = "\e[0m";

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

        var env = builder.Build();
        try
        {
            PyInterpreter.RunCode(env, code,
                moduleName: Path.GetFileNameWithoutExtension(path),
                sourceName: fullPath);
            return 0;
        }
        catch (PyRuntimeException e)
        {
            return HandlePyError(e, env);
        }
        finally
        {
            env.Dispose();
        }
    }

    private static int RunCode(IPyEnvironmentBuilder builder, string code, string[] extraArgs)
    {
        builder
            .AddArg("-c")
            .AddArgs(extraArgs);

        var env = builder.Build();
        try
        {
            PyInterpreter.RunCode(env, code, sourceName: "-c");
            return 0;
        }
        catch (PyRuntimeException e)
        {
            return HandlePyError(e, env);
        }
        finally
        {
            env.Dispose();
        }
    }

    private static int RunRepl(IPyEnvironmentBuilder builder)
    {
        builder.SetInteractive(true);
        var env = builder.Build();
        try
        {
            PyInterpreter.RunRepl(env);
            return 0;
        }
        finally
        {
            env.Dispose();
        }
    }

    private static int HandlePyError(PyRuntimeException e, PyEnvironment env)
    {
        var exception = e.PyException;

        // SystemExit terminates normally; its argument carries the exit code.
        if (PySystemExitObjectType.Shared.IsInstance(exception))
            return GetSystemExitCode(exception);

        var message = e.Message;
        if (env.ErrorSupportsColor)
            System.Console.Error.WriteLine($"{AnsiColorRed}{message}{AnsiClearColor}");
        else
            System.Console.Error.WriteLine(message);
        return 1;
    }

    private static int GetSystemExitCode(PyExceptionObject exception)
    {
        if (exception.Args.Count is 0)
            return 0;

        return exception.Args[0] switch
        {
            PyIntObject i when i.IsInt32 => i.Int32Value,
            PyIntObject => 1,
            _ => 0,
        };
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
