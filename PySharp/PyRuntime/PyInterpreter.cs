using PySharp.AstNodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Environments;
using PySharp.Tokenization;
using System.Diagnostics;
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

    public static PyModuleObject RunFile(string filename, PyEnvironment? environment = null)
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

    public static PyModuleObject RunCode(string code, string? moduleName = null, PyEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        environment ??= PyEnvironment
            .CreateBuilder()
            .StandardIO.WithConsole()
            .FileSystem.WithEmptyMemoryFileSystem()
            .Build();

        var tokens = Tokenize(code);
        var node = Parse(tokens, environment);
        return PyVirtualMachine.ExecuteAstNode(node, moduleName ?? string.Empty, environment);
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
