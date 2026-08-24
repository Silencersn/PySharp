using PySharp.Compilation.CodeAnalysis;
using PySharp.Modules.Builtins;
using PySharp.Runtime;

namespace PySharp.Runtime.Calls;

partial class PyCallContext
{
    public PyResult Warn(PyObject message, PyExceptionType? warningType = null, int stacklevel = 1)
    {
        var typeResult = ResolveWarningType(message, warningType);
        if (typeResult.IsError)
            return typeResult.ExceptionResult;
        warningType = (PyExceptionType)typeResult.Value;

        var strResult = PySpecialMethods.Str(this, message);
        if (strResult.IsError)
            return strResult.ExceptionResult;

        var (filename, lineno, sourceLine) = ResolveWarningLocation(stacklevel);
        WriteWarning(filename, lineno, warningType, strResult.Value.Value, sourceLine);
        return default;
    }

    public PyResult WarnExplicit(PyObject message, PyExceptionType? warningType, string filename, int lineno, string? line = null)
    {
        var typeResult = ResolveWarningType(message, warningType);
        if (typeResult.IsError)
            return typeResult.ExceptionResult;
        warningType = (PyExceptionType)typeResult.Value;

        var strResult = PySpecialMethods.Str(this, message);
        if (strResult.IsError)
            return strResult.ExceptionResult;

        WriteWarning(filename, lineno, warningType, strResult.Value.Value, line);
        return default;
    }

    private PyResult ResolveWarningType(PyObject message, PyExceptionType? warningType)
    {
        // When the message is already a Warning instance, CPython derives the category
        // from its concrete type.
        if (warningType is null && PyWarningObjectType.Shared.IsInstance(message))
            warningType = message.PyType as PyExceptionType;

        warningType ??= PyUserWarningObjectType.Shared;

        if (!warningType.IsSubclassOf(PyWarningObjectType.Shared))
            return PyResult.TypeError($"category must be a Warning subclass, not '{warningType.Name}'");

        return warningType;
    }

    private (string Filename, int Lineno, string? SourceLine) ResolveWarningLocation(int stacklevel)
    {
        var frameState = _state;
        if (frameState is null || frameState.CurrentFrameCount is 0)
            return ("<sys>", 0, null);

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
            return ("<sys>", 0, null);

        var info = code.Bytecode.LineTable.Read(frame.InstructionIndex);
        int lineno = info is not null ? info.Start.Line : 0;
        string? sourceLine = info is not null ? info.FirstLine.ToString() : null;
        return (code.Filename, lineno, sourceLine);
    }

    // Writes the standard warning format "filename:lineno: Category: message", followed by the
    // source line (when available) indented by two spaces.
    private void WriteWarning(string filename, int lineno, PyExceptionType warningType, string text, string? sourceLine)
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
        WriteWarning(filename, lineno, PySyntaxWarningObjectType.Shared, message, sourceLine);
        return default;
    }
}

