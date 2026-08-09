using PySharp.Modules.Builtins;
using PySharp.Runtime;

namespace PySharp.Console;

internal static class Program
{
    private const string AnsiColorRed = "\e[31m";
    private const string AnsiClearColor = "\e[0m";

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            // No arguments: enter the interactive REPL.
            PyInterpreter.RunRepl();
            return 0;
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
                if (args.Length > 2)
                {
                    Error($"unexpected argument '{args[2]}' after -c: extra arguments are not passed through to sys.argv");
                    return 2;
                }
                return RunCode(args[1]);

            default:
                if (args[0].StartsWith('-'))
                {
                    Error($"unknown option: {args[0]}");
                    return 2;
                }
                if (args.Length > 1)
                {
                    Error($"unexpected argument '{args[1]}' after script: extra arguments are not passed through to sys.argv");
                    return 2;
                }
                return RunScript(args[0]);
        }
    }

    private static int RunScript(string path)
    {
        if (!File.Exists(path))
        {
            Error($"can't open file '{path}': No such file or directory");
            return 2;
        }

        try
        {
            PyInterpreter.RunFile(path);
            return 0;
        }
        catch (PyRuntimeException e)
        {
            return HandlePyError(e);
        }
    }

    private static int RunCode(string code)
    {
        try
        {
            PyInterpreter.RunCode(code);
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
        {
            return GetSystemExitCode(exception);
        }

        System.Console.Error.WriteLine($"{AnsiColorRed}{e.Message}{AnsiClearColor}");
        return 1;
    }

    private static int GetSystemExitCode(PyExceptionObject exception)
    {
        if (exception.Args.Count == 0)
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
            Usage: pysharp [options] [script]

            Options:
              -h, --help     show this help message and exit
              -V, --version  print PySharp version and exit
              -c <code>      run the given code as a Python program
            """);
    }

    private static void Error(string message)
    {
        System.Console.Error.WriteLine($"pysharp: {message}");
    }
}
