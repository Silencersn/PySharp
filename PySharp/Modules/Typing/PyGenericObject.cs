using PySharp.Modules.Builtins;

namespace PySharp.Modules.Typing;

/// <summary>
/// Represents <c>typing.Generic</c> — the abstract base class for generic types.
/// In CPython, <c>class Foo[T]:</c> implicitly inherits from <c>Generic[T]</c>.
/// This provides <c>__class_getitem__</c> for subscript support.
/// </summary>
internal sealed class PyGenericObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyGenericObjectType.Shared;
}
