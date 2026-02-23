using PySharp.Compilation.CodeAnalysis;
using PySharp.Runtime;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

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

[PyType("traceback")]
internal sealed partial class PyTracebackObjectType : PyTypeObject<PyTracebackObjectType, PyTracebackObject>
{
}
