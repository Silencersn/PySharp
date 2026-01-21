using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Comparison;

namespace PySharp.PyModules.Builtins;

public class PySetObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly HashSet<PyObject> _set;

    public override PyTypeObject DefaultPyType => PySetObjectType.Shared;

    public PySetObject()
    {
        _set = new HashSet<PyObject>(PyObjectComparer.Default);
    }
    public PySetObject(IEnumerable<PyObject> set)
    {
        _set = new HashSet<PyObject>(set, PyObjectComparer.Default);
    }

    PyResult IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(context, this, _set, "{", "}", ids);
    }
}

public sealed class PySetObjectType : PyTypeObject<PySetObjectType, PySetObject>
{
    public override string Module => "builtins";
    public override string Name => "set";

    protected override PyResult Repr(PyCallContext context, PySetObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Bool(PyCallContext context, PySetObject self)
    {
        return PyBoolObject.FromBoolean(self._set.Count > 0);
    }

    protected override PyResult Contains(PyCallContext context, PySetObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self._set.Contains(item));
    }
}
