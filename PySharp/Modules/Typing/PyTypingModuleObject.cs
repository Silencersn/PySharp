using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

/// <summary>
/// The <c>typing</c> module.
/// Provides runtime support for type hints including <c>Generic</c>.
/// </summary>
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyGenericObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyTypeVarObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyTypeVarTupleObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyParamSpecObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyParamSpecArgsObjectType))]
[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyParamSpecKwargsObjectType))]
public sealed partial class PyTypingModuleObject : PyModuleObject
{
    public PyTypingModuleObject() : base("typing")
    {
    }
}
