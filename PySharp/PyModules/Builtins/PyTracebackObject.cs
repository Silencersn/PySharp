using PySharp.CodeAnalysis;
using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

internal sealed class PyTracebackObject : PyObject
{
    internal readonly PyFrame _frame;
    internal readonly CodeMetaInfo? _info;
    internal readonly ICodeMetaInfoProvider? _infoProvider;

    public override PyTypeObject DefaultPyType => PyTracebackObjectType.Shared;

    internal PyTracebackObject(PyFrame frame, CodeMetaInfo? info, ICodeMetaInfoProvider? infoProvider)
    {
        _frame = frame;
        _info = info;
        _infoProvider = infoProvider;
    }
}

internal sealed class PyTracebackObjectType : PyTypeObject<PyTracebackObjectType, PyTracebackObject>
{
    public override string Name => "traceback";
}
