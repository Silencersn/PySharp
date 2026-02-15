using PySharp.AstNodes;
using PySharp.Bytecodes;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Environments;
using PySharp.Tokenization;
using System.Diagnostics;
using System.Text;

namespace PySharp.PyRuntime;

public class PyInterpreter
{
    private readonly PyEnvironment _environment;
    private readonly PyModuleObject _mainModule;
    private readonly PyCallContext _mainContext;

    internal PyEnvironment PyEnvironment => _environment;
    internal PyCallContext MainContext => _mainContext;

    private PyInterpreter(PyEnvironment environment)
    {
        _environment = environment;
        _mainModule = new PyModuleObject(PySpecialNames.Main);
        _mainContext = PyCallContext.CreateInterpreterMainContext(this);
        _mainModule._pyAttributes = _mainContext.CurrentFrame.Variables._globals.Globals;
    }

    public static PyInterpreter Create(PyEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return new PyInterpreter(environment);
    }

    internal static void InternalExecute(PyCallContext context, string code, string sourceName)
    {
        var codeObj = Compiler.CompileExec(context, code, sourceName);
        _ = new BytecodeVirtualMachine(context, codeObj.Bytecode).Eval().PyUnwrap(context);
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

    internal static PyModuleObject RunCodeWithContext(PyCallContext context, string code, string moduleName, string sourceName)
    {
        var codeObj = Compiler.CompileExec(context, code, sourceName);
        return PyVirtualMachine.Execute(context, codeObj);
    }

    internal static void RunCodeWithContext(PyCallContext context, string code, PyModuleObject module, string sourceName)
    {
        var codeObj = Compiler.CompileExec(context, code, sourceName);
        PyVirtualMachine.ExecuteToObject(context, codeObj.Bytecode, module);
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
        var context = interpreter._mainContext;
        var builder = new StringBuilder();

        while (true)
        {
            PyTryCatch(context, () =>
            {
                PyCodeObject codeObj;

                bool isFirstLine = true;
                builder.Clear();

                while (true)
                {
                    environment.Out.Write(isFirstLine ? ">>> " : "... ");
                    var line = environment.In.ReadLine() ?? throw new EndOfStreamException();
                    builder.AppendLine(line);
                    isFirstLine = false;

                    try
                    {
                        codeObj = Compiler.CompileSingle(context, builder.ToString(), "<stdin>", string.IsNullOrWhiteSpace(line));
                        break;
                    }
                    catch (PyRuntimeException e)
                    {
                        if (!PySyntaxErrorObjectType.Shared.IsInstance(e.PyException))
                            throw;

                        // TODO: currently, depend on implementation details
                        if (context.CurrentFrame.MetaInfoProvider is not Parser parser)
                            throw;

                        if (PyIndentationErrorObjectType.Shared.IsInstance(e.PyException))
                        {
                            while (parser.CurrentTokenType is TokenType.Dedent)
                                parser.MoveNextToken();
                        }

                        if (parser.CurrentTokenType is not TokenType.EndMarker)
                            throw;
                    }
                }

                _ = new BytecodeVirtualMachine(context, codeObj.Bytecode).Eval().PyUnwrap(context);
                Debug.Assert(context.CurrentFrame.IsRoot);
            });
        }
    }
}
