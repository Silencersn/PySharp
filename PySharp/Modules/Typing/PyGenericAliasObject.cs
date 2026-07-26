using PySharp.Modules.Builtins;

namespace PySharp.Modules.Typing;

/// <summary>
/// Represents a parameterized generic type, e.g. <c>list[int]</c> or <c>MyClass[T]</c>.
/// Corresponds to CPython's <c>types.GenericAlias</c> (gaobject).
/// </summary>
internal sealed class PyGenericAliasObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyGenericAliasObjectType.Shared;

    internal readonly PyObject _origin;
    internal readonly PyTupleObject _args;

    internal PyGenericAliasObject(PyObject origin, PyTupleObject args)
    {
        _origin = origin;
        _args = args;
    }
}
