using PySharp.AstNodes;
using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Environments;
using PySharp.Tokenization;
using System.Diagnostics;

namespace PySharp.PyRuntime;

public class PyInterpreter
{
    private readonly PyEnvironment _environment;
    private readonly PyModuleObject _mainModule;
    private readonly PyCallContext _mainContext;

    internal PyEnvironment PyEnvironment => _environment;

    private PyInterpreter(PyEnvironment environment)
    {
        _environment = environment;
        _mainModule = new PyModuleObject(PySpecialNames.Main);
        _mainContext = PyCallContext.CreateInterpreterMainContext(this);
        _mainModule._pyAttributes = _mainContext.CurrentFrame._globals.Globals;
    }

    public static PyInterpreter Create(PyEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return new PyInterpreter(environment);
    }

    private void ExecuteNode(AstModNode node)
    {
        node.Execute(_mainContext, _mainContext.CurrentFrame);
    }

    internal static void InternalExecute(PyCallContext context, string code, string sourceName)
    {
        var source = new CodeSource(sourceName, code);
        var tokens = Lexer.Tokenize(context, source);
        var node = Parser.ParseModule(context, source, tokens);
        SemanticAnalyzer.Analyze(context, node);
        node.Execute(context, context.CurrentFrame);
    }

    public void Execute(string code, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(sourceName);

        PyTryCatch(_mainContext, () =>
        {
            InternalExecute(_mainContext, code, sourceName);
        }, alwaysThrow: true);
    }

    public PyModuleObject GetModule(string moduleName)
    {
        ArgumentNullException.ThrowIfNull(moduleName);

        return new PyModuleObject(moduleName) { _pyAttributes = new Dictionary<string, PyObject>(_mainModule.PyAttributes) };
    }

    internal static void PyTryCatch(PyCallContext context, Action action, bool alwaysThrow = false)
    {
        var frame = context.CurrentFrame;
        try
        {
            action();
        }
        catch (Exception e)
        {
            var currentException = e;
            while (currentException is not null)
            {
                if (currentException is PyRuntimeException pyRuntimeException)
                {
                    var exc = pyRuntimeException.PyException.WithTraceback(context, overwriteExisting: false);
                    context.EnsureFrameState(frame);

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

                    if (alwaysThrow)
                        throw;

                    return;
                }

                currentException = currentException.InnerException;
            }

            Debug.Assert(currentException is null);
            // If no PyRuntimeException was found in the exception chain,
            // re-throw the original exception (non-Python errors).
            throw;
        }
    }

    public static PyModuleObject RunFile(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);

        var code = File.ReadAllText(filename);
        var moduleName = Path.GetFileNameWithoutExtension(filename);
        var environment = PyEnvironment
            .CreateBuilder()
            .StandardIO.WithConsole()
            .FileSystem.WithPhysicalFileSystem()
            .System.AppendSysPath(Path.GetDirectoryName(Path.GetFullPath(filename))).AppendArgument(filename)
            .Build();

        var interpreter = Create(environment);
        interpreter.Execute(code, Path.GetFullPath(filename));
        return interpreter.GetModule(moduleName);
    }

    public static PyModuleObject? RunCode(string code, string? moduleName = null, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(code);

        var environment = PyEnvironment
            .CreateBuilder()
            .StandardIO.WithConsole()
            .FileSystem.WithEmptyMemoryFileSystem()
            .Build();

        var interpreter = Create(environment);
        interpreter.Execute(code, sourceName ?? "<string>");
        return moduleName is not null ? interpreter.GetModule(moduleName) : null;
    }

    internal static PyModuleObject RunCodeWithinEnvironment(PyCallContext context, string code, string moduleName, string sourceName)
    {
        var codeSource = new CodeSource(sourceName, code);
        var tokens = Lexer.Tokenize(context, codeSource);
        var node = Parser.ParseModule(context, codeSource, tokens);
        SemanticAnalyzer.Analyze(context, node);
        return PyVirtualMachine.Execute(context, node, moduleName);
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

        var interpreter = Create(environment);

        while (true)
        {
            PyTryCatch(interpreter._mainContext, () =>
            {
                InteractiveNode node;

                var codeSource = new CodeSource("<stdin>", string.Empty);
                var lexer = new Lexer(interpreter._mainContext, codeSource);
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

                    var parser = new Parser(interpreter._mainContext, codeSource, lexer.Tokens);
                    try
                    {
                        node = parser.ParseInteractiveNode();
                        SemanticAnalyzer.Analyze(interpreter._mainContext, node);
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

                interpreter.ExecuteNode(node);
                Debug.Assert(interpreter._mainContext.CurrentFrame.IsRoot);
            });
        }
    }
}
