using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

/// <summary>
/// Represents a type parameter tuple created from the PEP 695 generic syntax
/// (e.g. <c>class C[*Ts]:</c>) or <c>typing.TypeVarTuple</c>.
/// Corresponds to CPython's <c>typing.TypeVarTuple</c> (typevarobject.c).
/// </summary>
internal sealed class PyTypeVarTupleObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyTypeVarTupleObjectType.Shared;

    internal readonly string _name;

    internal PyTypeVarTupleObject(string name)
    {
        _name = name;
    }
}

[PyType("TypeVarTuple", Module = "typing")]
[AIGenerated]
internal sealed partial class PyTypeVarTupleObjectType : PyTypeObject<PyTypeVarTupleObject>
{
    // CPython typevartuple_repr: the repr is just the name
    protected override PyResult Repr(PyCallContext context, PyTypeVarTupleObject self)
    {
        return PyStrObject.FromString(self._name);
    }

    // CPython typevartuple_new: only the name is required; the default
    // keyword is not yet supported here
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError("TypeVarTuple() takes no keyword arguments");

        if (args.Count is not 1 || args[0] is not PyStrObject name)
            return PyResult.TypeError("TypeVarTuple() argument 'name' must be str");

        return new PyTypeVarTupleObject(name.Value);
    }

    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyTypeVarTupleObject self)
    {
        return PyStrObject.FromString(self._name);
    }

    [PyProperty("__default__")]
    private static PyResult Get_Default(PyCallContext context, PyTypeVarTupleObject self)
    {
        return PyNoneObject.None;
    }
}
