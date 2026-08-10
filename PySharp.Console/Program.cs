using PySharp.Modules.Builtins;
using PySharp.Runtime;

namespace PySharp.Console;

internal static class Program
{
    private const string AnsiColorRed = "\e[31m";
    private const string AnsiClearColor = "\e[0m";

    private static int Main(string[] args)
    {
        if (args.Length is 0)
        {
            // No arguments: enter the interactive REPL.
            PyInterpreter.RunRepl();
            return 0;
        }

        // "--" terminates option parsing: everything after it is treated as
        // the script and its arguments (matches CPython).
        if (args[0] is "--")
        {
            // "pysharp --" with no script: enter the interactive REPL.
            if (args.Length is 1)
            {
                PyInterpreter.RunRepl();
                return 0;
            }
            return RunScript(args[1], args[2..]);
        }

        switch (args[0])
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
                if (args.Length < 2)
                {
                    Error("argument -c: expected one argument");
                    return 2;
                }
                // Extra arguments after the code are passed through to sys.argv
                // (matches CPython: sys.argv == ['-c', ...]).
                return RunCode(args[1], args[2..]);

            default:
                if (args[0].StartsWith('-'))
                {
                    Error($"unknown option: {args[0]}");
                    return 2;
                }
                // Extra arguments after the script are passed through to sys.argv
                // (matches CPython: sys.argv == ['script.py', ...]).
                return RunScript(args[0], args[1..]);
        }
    }

    private static int RunScript(string path, string[] scriptArgs)
    {
        if (!File.Exists(path))
        {
            Error($"can't open file '{path}': No such file or directory");
            return 2;
        }

        try
        {
            PyInterpreter.RunFile(path, scriptArgs);
            return 0;
        }
        catch (PyRuntimeException e)
        {
            return HandlePyError(e);
        }
    }

    private static int RunCode(string code, string[] extraArgs)
    {
        try
        {
            PyInterpreter.RunCode(code, args: extraArgs);
            return 0;
        }
        catch (PyRuntimeException e)
        {
            return HandlePyError(e);
        }
    }

    private static int HandlePyError(PyRuntimeException e)
    {
        var exception = e.PyException;

        // SystemExit terminates normally; its argument carries the exit code.
        if (PySystemExitObjectType.Shared.IsInstance(exception))
            return GetSystemExitCode(exception);

        System.Console.Error.WriteLine($"{AnsiColorRed}{e.Message}{AnsiClearColor}");
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
