using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Environments;
using PySharp.Tokenization;
using System.Diagnostics;

namespace PySharp.PyRuntime;

public static class PyInterpreter
{
    public static IReadOnlyList<TokenInfo> Tokenize(string code)
    {
        return Lexer.Tokenize(code);
    }

    internal static void PyTryCatch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            var currentException = ex;
            while (currentException is not null)
            {
                if (currentException is PyRuntimeException pyRuntimeException)
                {
                    PyVirtualMachine.CurrentException ??= pyRuntimeException.PyException;
                    PyVirtualMachine.CurrentException.WithTraceback();

                    if (pyRuntimeException.PyException.PyType == PyStandardExceptionTypes.SystemExit)
                    {
                        PyVirtualMachine.ClearException();
                    }
                    else if (PyVirtualMachine.PyEnvironment.ExitCode is 0)
                    {
                        PyVirtualMachine.PyEnvironment.ExitCode = 1;
                        var color = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        PyVirtualMachine.Error.WriteLine(PyVirtualMachine.CurrentException.ToMessage());
                        Console.ForegroundColor = color;
                    }
                    break;
                }

                currentException = currentException.InnerException;
            }

            if (currentException is null)
                // If no PyRuntimeException was found in the exception chain,
                // re-throw the original exception (non-Python errors).
                throw;
        }
    }

    public static PyModuleObject? RunFile(string filename, PyEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(filename);

        var code = File.ReadAllText(filename);
        var moduleName = Path.GetFileNameWithoutExtension(filename);
        environment ??= PyEnvironment
            .CreateBuilder()
            .StandardIO.WithConsole()
            .FileSystem.WithPhysicalFileSystem()
            .System.AppendSysPath(Path.GetDirectoryName(Path.GetFullPath(filename))).AppendArgument(filename)
            .Build();

        return RunCode(code, moduleName, environment, Path.GetFullPath(filename));
    }

    public static PyModuleObject? RunCode(string code, string? moduleName = null, PyEnvironment? environment = null, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        environment ??= PyEnvironment
            .CreateBuilder()
            .StandardIO.WithConsole()
            .FileSystem.WithEmptyMemoryFileSystem()
            .Build();

        using var context = new PyEnvironmentContext(environment);

        PyModuleObject? module = null;
        PyTryCatch(() =>
        {
            module = RunCodeWithinEnvironment(code, moduleName ?? string.Empty, false, sourceName ?? "<string>");
            Debug.Assert(PyVirtualMachine.CurrentFrame.IsRoot);
        });
        PyVirtualMachine.PyEnvironment.OnExit();
        return module;
    }

    public static PyModuleObject RunCodeWithinEnvironment(string code, string moduleName, bool newFrame, string sourceName)
    {
        var tokens = Tokenize(code);
        var node = Parser.Parse(sourceName, tokens, PyVirtualMachine.PyEnvironment);
        return PyVirtualMachine.Execute(node, moduleName, newFrame);
    }

    public static void RunRepl()
    {
        var environment = PyEnvironment
            .CreateBuilder()
            .StandardIO.WithConsole()
            .InterpreterMode.Interactive()
            .Initialization.SyncExit()
            .Build();

        environment.Out.WriteLine($"{nameof(PySharp)} (v{typeof(PyInterpreter).Assembly.GetName().Version}) on {Environment.OSVersion}");


        using var context = new PyEnvironmentContext(environment);

        var tokenStream = new TokenInteractiveStream(environment.In, environment.Out);
        var parser = new Parser("<stdin>", tokenStream, environment.OptimizationOptions);

        while (true)
        {
            InteractiveNode node;
            try
            {
                node = parser.ParseInteractiveNode();
            }
            catch (PyRuntimeException e)
            {
                environment.Error.WriteLine(e.Message);
                tokenStream = new TokenInteractiveStream(environment.In, environment.Out);
                parser = new Parser("<stdin>", tokenStream, environment.OptimizationOptions);
                continue;
            }

            PyTryCatch(() =>
            {
                node.Execute(PyVirtualMachine.CurrentFrame);
                Debug.Assert(PyVirtualMachine.CurrentFrame.IsRoot);
            });
        }
    }
}
