using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
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

    internal static void PyTryCatch(PyCallContext context, Action action)
    {
        var frame = context.CurrentFrame;
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
                    context.CurrentException ??= pyRuntimeException.PyException;
                    context.CurrentException.WithTraceback(context);

                    if (pyRuntimeException.PyException.PyType == PyStandardExceptionTypes.SystemExit)
                    {
                        context.ClearException();
                    }
                    else
                    {
                        if (context.PyEnvironment.ExitCode is 0)
                            context.PyEnvironment.ExitCode = 1;
                        var color = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        context.Error.WriteLine(context.CurrentException.ToMessage(context));
                        Console.ForegroundColor = color;
                    }

                    while (context.CurrentFrame != frame)
                        context.ExitFrame();

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

        using var _ = new PyEnvironmentContext(environment);
        var context = PyCallContext.FromEnvironment(environment);

        PyModuleObject? module = null;
        PyTryCatch(context, () =>
        {
            module = RunCodeWithinEnvironment(context, code, moduleName ?? string.Empty, false, sourceName ?? "<string>");
            Debug.Assert(context.CurrentFrame.IsRoot);
        });
        context.PyEnvironment.OnExit();
        return module;
    }

    public static PyModuleObject RunCodeWithinEnvironment(PyCallContext context, string code, string moduleName, bool newFrame, string sourceName)
    {
        var tokens = Tokenize(code);
        var node = Parser.Parse(sourceName, tokens, context);
        return PyVirtualMachine.Execute(context, node, moduleName, newFrame);
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


        using var _ = new PyEnvironmentContext(environment);
        var context = PyCallContext.FromEnvironment(environment);

        while (true)
        {
            InteractiveNode node;
            try
            {
                var tokenStream = new TokenInteractiveStream(environment.In, environment.Out);
                var parser = new Parser(context, "<stdin>", tokenStream, environment.OptimizationOptions);
                node = parser.ParseInteractiveNode();
            }
            catch (PyRuntimeException e)
            {
                environment.Error.WriteLine(e.Message);
                continue;
            }

            PyTryCatch(context, () =>
            {
                node.Execute(context, context.CurrentFrame);
                Debug.Assert(context.CurrentFrame.IsRoot);
            });
        }
    }
}
