using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime;

namespace PySharp.Runtime.Calls;

partial class PyCallContext
{
    public PyResult Warn(PyObject message, PyTypeObject<PyExceptionObject>? warningType = null, int stacklevel = 1)
    {
        var typeResult = ResolveWarningType(message, warningType);
        if (typeResult.IsError)
            return typeResult.ExceptionResult;
        warningType = (PyTypeObject<PyExceptionObject>)typeResult.Value;

        var strResult = PySpecialMethods.Str(this, message);
        if (strResult.IsError)
            return strResult.ExceptionResult;

        var (filename, lineno, sourceLine, module) = ResolveWarningLocation(stacklevel);
        return DispatchWarning(filename, lineno, warningType, strResult.Value.Value, module, sourceLine);
    }

    public PyResult WarnExplicit(PyObject message, PyTypeObject<PyExceptionObject>? warningType, string filename, int lineno, string? line = null)
    {
        var typeResult = ResolveWarningType(message, warningType);
        if (typeResult.IsError)
            return typeResult.ExceptionResult;
        warningType = (PyTypeObject<PyExceptionObject>)typeResult.Value;

        var strResult = PySpecialMethods.Str(this, message);
        if (strResult.IsError)
            return strResult.ExceptionResult;

        return DispatchWarning(filename, lineno, warningType, strResult.Value.Value, NormalizeModule(filename), line);
    }

    // Resolves the filter action and dispatches: "error" raises, "ignore" suppresses, and
    // "default" writes the warning once per (module, text, category, lineno) site.
    private PyResult DispatchWarning(string filename, int lineno, PyTypeObject<PyExceptionObject> warningType, string text, string module, string? sourceLine)
    {
        var state = PyEnvironment.Warnings;
        var action = state.ResolveAction(warningType);
        switch (action)
        {
            case WarningAction.Error:
                return PyResult.RaiseException(warningType, PyStrObject.FromString(text));
            case WarningAction.Ignore:
                return default;
            default:
                if (state.ShouldSuppress(module, text, warningType, lineno))
                    return default;
                WriteWarning(filename, lineno, warningType, text, sourceLine);
                state.MarkWarned(module, text, warningType, lineno);
                return default;
        }
    }

    // Derives the registry module name from a filename the same way CPython's normalize_module
    // does: an empty name maps to "<unknown>", and a trailing ".py" is stripped.
    private static string NormalizeModule(string filename)
    {
        if (filename.Length is 0)
            return "<unknown>";
        if (filename.Length >= 3 && filename.EndsWith(".py", StringComparison.Ordinal))
            return filename[..^3];
        return filename;
    }

    private PyResult ResolveWarningType(PyObject message, PyTypeObject<PyExceptionObject>? warningType)
    {
        // When the message is already a Warning instance, CPython derives the category
        // from its concrete type.
        if (warningType is null && PyWarningObjectType.Shared.IsInstance(message))
            warningType = message.PyType as PyTypeObject<PyExceptionObject>;

        warningType ??= PyUserWarningObjectType.Shared;

        if (!warningType.IsSubclassOf(PyWarningObjectType.Shared))
            return PyResult.TypeError($"category must be a Warning subclass, not '{warningType.Name}'");

        return warningType;
    }

    private (string Filename, int Lineno, string? SourceLine, string Module) ResolveWarningLocation(int stacklevel)
    {
        var frameState = _state;
        if (frameState is null || frameState.CurrentFrameCount is 0)
            return ("<sys>", 0, null, "<sys>");

        // Comprehension frames are inline, so they are skipped when walking the logical stack.
        int idx = frameState.CurrentFrameCount - 1;
        while (idx > 0 && frameState.GetFrame(idx).FrameType is FrameType.Comprehension)
            idx--;

        int remaining = Math.Max(0, stacklevel - 1);
        while (idx > 0 && remaining > 0)
        {
            idx--;
            while (idx > 0 && frameState.GetFrame(idx).FrameType is FrameType.Comprehension)
                idx--;
            remaining--;
        }

        ref var frame = ref frameState.GetFrame(idx);
        var code = frame.CodeObject;
        if (code is null)
            return ("<sys>", 0, null, "<sys>");

        var info = code.Bytecode.LineTable.Read(frame.InstructionIndex);
        int lineno = info is not null ? info.Start.Line : 0;
        string? sourceLine = info is not null ? info.FirstLine.ToString() : null;
        string module = ResolveModuleName(ref frame);
        return (code.Filename, lineno, sourceLine, module);
    }

    // Reads the module name from the target frame's globals, falling back to a normalized form
    // of the code object's filename for frames that do not expose a module name.
    private static string ResolveModuleName(ref PyInternalFrame frame)
    {
        if (frame.Variables?.Globals is { } globals &&
            globals.TryGetValue(PySpecialNames.Name, out var name) && name is PyStrObject nameStr)
            return nameStr.Value;
        return NormalizeModule(frame.CodeObject?.Filename ?? "<sys>");
    }

    // Writes the standard warning format "filename:lineno: Category: message", followed by the
    // source line (when available) indented by two spaces.
    private void WriteWarning(string filename, int lineno, PyTypeObject<PyExceptionObject> warningType, string text, string? sourceLine)
    {
        var error = PyEnvironment.Error;
        error.WriteLine($"{filename}:{lineno}: {warningType.Name}: {text}");
        if (!string.IsNullOrEmpty(sourceLine))
            error.WriteLine($"  {sourceLine}");
    }

    public PyResult Warn(PyExceptionType warningType, string message)
        => Warn(warningType, message, stacklevel: 1);

    public PyResult Warn(PyExceptionType warningType, string message, int stacklevel)
        => Warn(PyStrObject.FromString(message), warningType, stacklevel);

    public PyResult Warn<TWarning>(string message) where TWarning : PyExceptionType, IPyException<TWarning>
        => Warn(TWarning.Shared, message);

    public PyResult Warn(PyExceptionType warningType, string message, string filename, int lineno, string? line = null)
        => WarnExplicit(PyStrObject.FromString(message), warningType, filename, lineno, line);

    internal PyResult WarnSyntax(string message, ICodeMetaInfoProvider provider)
    {
        var info = provider.MetaInfo;
        string filename = info?.Source?.Name ?? "<unknown>";
        int lineno = info is null ? 0 : info.Start.Line;
        string? sourceLine = info is null ? null : info.FirstLine.ToString().Trim();
        return WarnExplicit(PyStrObject.FromString(message), PySyntaxWarningObjectType.Shared, filename, lineno, sourceLine);
    }
}

