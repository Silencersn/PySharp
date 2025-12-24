using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PySetObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly HashSet<PyObject> _set;

    public override PyTypeObject DefaultPyType => PySetObjectType.Shared;

    public PySetObject()
    {
        _set = new HashSet<PyObject>(PyObjectRuntimeEqualityComparer.Shared);
    }
    public PySetObject(IEnumerable<PyObject> set)
    {
        _set = new HashSet<PyObject>(set, PyObjectRuntimeEqualityComparer.Shared);
    }

    PyResult IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(context, this, _set, "{", "}", ids);
    }
}

public sealed class PySetObjectType : PyTypeObject<PySetObjectType, PySetObject>
{
    public override string Name => "set";

    protected internal override PyResult Repr(PyCallContext context, PySetObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected internal override PyResult Bool(PyCallContext context, PySetObject self)
    {
        return PyBoolObject.FromBoolean(self._set.Count > 0);
    }
}
