using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

/// <summary>
/// Represents a type parameter created from the PEP 695 generic syntax (e.g. <c>class C[T]:</c>).
/// Provides <c>__name__</c> for identification. Bound, constraints, and default are not yet supported.
/// Corresponds to CPython's <c>typing.TypeVar</c> (typevarobject).
/// </summary>
internal sealed class PyTypeVarObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyTypeVarObjectType.Shared;

    internal readonly string _name;

    internal PyTypeVarObject(string name)
    {
        _name = name;
    }
}

[PyType("TypeVar", Module = "typing")]
[AIGenerated]
internal sealed partial class PyTypeVarObjectType : PyTypeObject<PyTypeVarObject>
{
    protected override PyResult Repr(PyCallContext context, PyTypeVarObject self)
    {
        // PEP 695 type params have infer_variance=true, so repr is just the name (no ~/+/- prefix)
        return PyStrObject.FromString(self._name);
    }

    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyTypeVarObject self)
    {
        return PyStrObject.FromString(self._name);
    }

    [PyProperty("__bound__")]
    private static PyResult Get_Bound(PyCallContext context, PyTypeVarObject self)
    {
        return PyNoneObject.None;
    }

    [PyProperty("__constraints__")]
    private static PyResult Get_Constraints(PyCallContext context, PyTypeVarObject self)
    {
        return PyTupleObject.Empty;
    }

    [PyProperty("__default__")]
    private static PyResult Get_Default(PyCallContext context, PyTypeVarObject self)
    {
        return PyNoneObject.None;
    }
}
