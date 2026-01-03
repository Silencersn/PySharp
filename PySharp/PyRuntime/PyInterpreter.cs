using PySharp.AstNodes;
using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Environments;
using PySharp.Tokenization;
using System.Diagnostics;

namespace PySharp.PyRuntime;

public static class PyInterpreter
{
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
                    var exc = pyRuntimeException.PyException.WithTraceback(context);

                    if (PyStandardExceptionTypes.SystemExit.IsInstance(exc))
                    {
                    }
                    else
                    {
                        if (context.PyEnvironment.ExitCode is 0)
                            context.PyEnvironment.ExitCode = 1;
                        var color = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        context.Error.WriteLine(exc.ToMessage(context));
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

        var context = PyCallContext.FromLoadingModule(environment);

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
        var codeSource = new CodeSource(sourceName, code);
        var tokens = Lexer.Tokenize(context, codeSource);
        var node = Parser.ParseModule(context, codeSource, tokens);
        SemanticAnalyzer.Analyze(context, node);
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


        var context = PyCallContext.FromLoadingModule(environment);

        while (true)
        {
            PyTryCatch(context, () =>
            {
                InteractiveNode node;

                var codeSource = new CodeSource("<stdin>", string.Empty);
                var lexer = new Lexer(context, codeSource);
                lexer.InternalStart();
                bool isFirstLine = true;
                while (true)
                {
                    environment.Out.Write(isFirstLine ? ">>> " : "... ");
                    var line = environment.In.ReadLine() ?? throw new EndOfStreamException();
                    isFirstLine = false;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        lexer.InternalClearIndentation();
                        lexer.Tokens.Add(new TokenInfo(TokenType.NewLine, string.Empty, default, default, codeSource));
                        lexer.Tokens.Add(new TokenInfo(TokenType.EndMarker, string.Empty, default, default, codeSource));
                    }
                    else
                    {
                        line += Environment.NewLine;
                        codeSource.Code.AppendText(line);
                        lexer.InternalTokenize(line);
                        lexer.Tokens.Add(new TokenInfo(TokenType.EndMarker, string.Empty, default, default, codeSource));
                    }

                    var parser = new Parser(context, codeSource, lexer.Tokens);
                    try
                    {
                        node = parser.ParseInteractiveNode();
                        break;
                    }
                    catch (PyRuntimeException e)
                    {
                        if (!PyStandardExceptionTypes.SyntaxError.IsInstance(e.PyException))
                            throw;

                        if (parser.CurrentToken.Type is not TokenType.EndMarker)
                            throw;

                        lexer.Tokens.RemoveAt(lexer.Tokens.Count - 1); // remove EndMarker
                    }
                }

                node.Execute(context, context.CurrentFrame);
                Debug.Assert(context.CurrentFrame.IsRoot);
            });
        }
    }
}
