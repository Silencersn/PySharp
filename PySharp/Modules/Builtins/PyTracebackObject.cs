using PySharp.Compilation.CodeAnalysis;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

internal sealed class PyTracebackObject : PyObject
{
    internal readonly CodeMetaInfo? _info;
    internal readonly ICodeMetaInfoProvider? _infoProvider;

    public override PyTypeObject DefaultPyType => PyTracebackObjectType.Shared;

    internal PyTracebackObject(CodeMetaInfo? info, ICodeMetaInfoProvider? infoProvider)
    {
        _info = info;
        _infoProvider = infoProvider;
    }
}

[PyType("traceback")]
internal sealed partial class PyTracebackObjectType : PyTypeObject<PyTracebackObjectType, PyTracebackObject>
{
}
