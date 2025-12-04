using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Environments;
using PySharp.Tokenization;
using System;
using System.Diagnostics;
using System.IO;

namespace PySharp.PyRuntime;

public static class PyInterpreter
{
    public static IReadOnlyList<TokenInfo> Tokenize(string code)
    {
        return Lexer.Tokenize(code);
    }

    public static ModuleNode Parse(IEnumerable<TokenInfo> tokens, PyEnvironment? environment = null)
    {
        return Parser.Parse(tokens, environment);
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

        return RunCode(code, moduleName, environment);
    }

    public static PyModuleObject? RunCode(string code, string? moduleName = null, PyEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        environment ??= PyEnvironment
            .CreateBuilder()
            .StandardIO.WithConsole()
            .FileSystem.WithEmptyMemoryFileSystem()
            .Build();

        using var context = new PyEnvironmentContext(environment);

        PyModuleObject? module = null;
        try
        {
            module = RunCodeWithinEnvironment(code, moduleName ?? string.Empty, false);
            Debug.Assert(PyVirtualMachine.CurrentFrame.IsRoot);
        }
        catch (Exception ex)
        {
            var currentException = ex;
            while (currentException is not null)
            {
                if (currentException is PyRuntimeException pyRuntimeException)
                {
                    PyVirtualMachine.CurrentException ??= pyRuntimeException.PyException;

                    if (pyRuntimeException.PyException.PyType == PyStandardExceptionTypes.SystemExit)
                    {
                        PyVirtualMachine.ClearException();
                    }
                    else if (PyVirtualMachine.PyEnvironment.ExitCode is 0)
                    {
                        PyVirtualMachine.PyEnvironment.ExitCode = 1;
                        PyVirtualMachine.Error.WriteLine(PyVirtualMachine.CurrentException.ToMessage());
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

        PyVirtualMachine.PyEnvironment.OnExit();
        return module;
    }

    public static PyModuleObject RunCodeWithinEnvironment(string code, string moduleName, bool newFrame)
    {
        var tokens = Tokenize(code);
        var node = Parse(tokens, PyVirtualMachine.PyEnvironment);
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
        var parser = new Parser(tokenStream, environment.OptimizationOptions);

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
                parser = new Parser(tokenStream, environment.OptimizationOptions);
                continue;
            }

            try
            {
                node.Execute(PyVirtualMachine.CurrentFrame);
                Debug.Assert(PyVirtualMachine.CurrentFrame.IsRoot);
            }
            catch (PyRuntimeException e)
            {
                if (e.PyException.PyType == PyStandardExceptionTypes.SystemExit)
                    return;
                environment.Error.WriteLine(e.Message);
            }
        }
    }
}
