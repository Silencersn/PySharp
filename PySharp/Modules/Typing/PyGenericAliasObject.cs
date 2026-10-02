using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;

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

    /// <summary>
    /// The <c>__class_getitem__</c> body the built-in container types
    /// (list, tuple, dict, set, frozenset) share, mirroring CPython's
    /// METH-O <c>Py_GenericAlias</c> (descrobject.c): the subscript key
    /// arrives as the single argument and becomes the args tuple, with
    /// tuple keys (e.g. <c>dict[int, str]</c>) used as-is.
    /// </summary>
    internal static PyResult ClassGetItem(PyTypeObject cls, PyArguments arguments)
    {
        // METH-O arity message, matching CPython's cfunction call format
        if (arguments.ExtraArgs.Count is not 1)
            return PyResult.TypeError($"{cls.Name}.{PySpecialNames.ClassGetItem}() takes exactly one argument ({arguments.ExtraArgs.Count} given)");

        var key = arguments.ExtraArgs[0];
        var argsTuple = key is PyTupleObject tuple ? tuple : PyTupleObject.CreateTuple([key]);
        return new PyGenericAliasObject(cls, argsTuple);
    }}
