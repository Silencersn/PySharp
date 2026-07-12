using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes;
using PySharp.Compilation.Tokenization;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using System.Diagnostics;
using System.Text;

namespace PySharp.Runtime;

public sealed class PyInterpreter : IDisposable
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
        _mainModule._pyAttributes = _mainContext.CurrentInternalFrame.Variables.Globals.Dict;
    }

    public static PyInterpreter Create(PyEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return new PyInterpreter(environment);
    }

    internal static void InternalExecute(PyCallContext context, string code, string sourceName)
    {
        var codeObj = Compiler.CompileExec(context, code, sourceName, name: "<module>");
        context.CurrentInternalFrame.CodeObject = codeObj;
        _ = PyCore.Eval(context).PyUnwrap(context);
    }

    internal static PyModuleObject InternalExecuteNewModule(PyCallContext context, PyCodeObject code, bool isMain)
    {
        var module = new PyModuleObject(code.Name);
        InternalExecuteToModule(context, code, module, isMain);
        return module;
    }
    internal static void InternalExecuteToModule(PyCallContext context, PyCodeObject code, PyModuleObject module, bool isMain)
    {
        // module will be reloaded
        var dict = context.CurrentInternalFrame.Variables.Globals.Dict;
        if (module._pyAttributes is not null)
        {
            foreach (var pair in module._pyAttributes)
                dict[pair.Key] = pair.Value;
        }
        module._pyAttributes = context.CurrentInternalFrame.Variables.Globals.Dict;
        module.PyAttributes[PySpecialNames.Name] = isMain ? PySpecialNames.Interned.Main : PyStrObject.FromString(module.Name);

        // Set __package__ correctly for the module
        if (isMain)
        {
            module.PyAttributes[PySpecialNames.Package] = PyNoneObject.None;
        }
        else if (module.PyAttributes.ContainsKey(PySpecialNames.Path))
        {
            // Package: __package__ == __name__
            module.PyAttributes[PySpecialNames.Package] = PyStrObject.FromString(module.Name);
        }
        else
        {
            // Non-package module: __package__ = parent package name
            var lastDot = module.Name.LastIndexOf('.');
            module.PyAttributes[PySpecialNames.Package] =
                lastDot >= 0 ? PyStrObject.FromString(module.Name[..lastDot]) : PyStrObject.Empty;
        }

        context.CurrentInternalFrame.CodeObject = code;
        _ = PyCore.Eval(context).PyUnwrap(context);
        Debug.Assert(ReferenceEquals(module.PyAttributes, context.CurrentInternalFrame.Variables.Globals.Dict));
        if (isMain)
            module.PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(module.Name);
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

    public PyModuleObject MakeModule(string moduleName)
    {
        ArgumentNullException.ThrowIfNull(moduleName);

        return new PyModuleObject(moduleName) { _pyAttributes = new Dictionary<string, PyObject>(_mainModule.PyAttributes) };
    }

    internal static void PyTryCatch(PyCallContext context, Action action, bool alwaysThrow = false)
    {
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

                    if (PySystemExitObjectType.Shared.IsInstance(exc))
                    {
                    }
                    else
                    {
                        if (context.PyEnvironment.ExitCode is 0)
                            context.PyEnvironment.ExitCode = 1;

                        const string ANSIColorRed = "\e[31m";
                        const string ANSIClearColor = "\e[0m";
                        context.Error.Write(ANSIColorRed);
                        context.Error.WriteLine(exc.ToMessage(context));
                        context.Error.Write(ANSIClearColor);
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
        var host = PyEnvironmentHost.CreateConsole(usingPhysicalFileSystem: true);

        var fullPath = Path.GetFullPath(filename);
        var scriptDirectory = Path.GetDirectoryName(fullPath)!;

        using var environment = PyEnvironment
            .CreateBuilder(host)
            .AddPath(scriptDirectory)
            .AddArg(filename)
            .Build();

        using var interpreter = Create(environment);
        return RunCodeWithContext(interpreter.MainContext, code, moduleName, fullPath, isMain: true);
    }

    public static PyModuleObject? RunCode(string code, string? moduleName = null, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        moduleName ??= "<module>";
        sourceName ??= "<string>";

        using var environment = PyEnvironment
            .CreateBuilder(PyEnvironmentHost.CreateConsole())
            .Build();

        using var interpreter = Create(environment);
        return RunCodeWithContext(interpreter.MainContext, code, moduleName, sourceName, isMain: true);
    }

    internal static PyModuleObject RunCodeWithContext(PyCallContext context, string code, string moduleName, string sourceName, bool isMain)
    {
        var codeObj = Compiler.CompileExec(context, code, sourceName, moduleName);
        return InternalExecuteNewModule(context, codeObj, isMain);
    }

    internal static void RunCodeWithContext(PyCallContext context, string code, PyModuleObject module, string sourceName, bool isMain)
    {
        var codeObj = Compiler.CompileExec(context, code, sourceName, module.Name);
        InternalExecuteToModule(context, codeObj, module, isMain);
    }

    public static void RunRepl()
    {
        using var environment = PyEnvironment
            .CreateBuilder(PyEnvironmentHost.CreateRepl())
            .SetInteractive(true)
            .Initialization.SyncExit()
            .Build();

        environment.Out.WriteLine($"{nameof(PySharp)} (v{typeof(PyInterpreter).Assembly.GetName().Version}) on {Environment.OSVersion}");

        using var interpreter = Create(environment);
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
                        codeObj = Compiler.CompileSingle(context, builder.ToString(), "<stdin>", name: "<module>", string.IsNullOrWhiteSpace(line));
                        break;
                    }
                    catch (PyRuntimeException e)
                    {
                        if (!PySyntaxErrorObjectType.Shared.IsInstance(e.PyException))
                            throw;

                        if (e.Compiler is not Parser parser)
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

                _ = PyCore.Eval(context).PyUnwrap(context);
                Debug.Assert(context.FrameState.CurrentFrameCount is 1);
            });
        }
    }

    public void Dispose()
    {
        _mainContext.Dispose();
        _environment.Dispose();
    }
}
