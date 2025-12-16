namespace PySharp.PyModules.Builtins;

public class PySetObject : PyObject, IPyObjectRecursiveRepr
{
    private readonly HashSet<PyObject> _set;

    public PySetObject()
    {
        _set = new HashSet<PyObject>(PyObjectRuntimeEqualityComparer.Shared);
    }
    public PySetObject(IEnumerable<PyObject> set)
    {
        _set = new HashSet<PyObject>(set, PyObjectRuntimeEqualityComparer.Shared);
    }

    protected internal override PyBoolObject BoolImpl()
    {
        return _set.Count > 0;
    }

    protected internal override PyObject? ReprImpl()
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(this);
    }

    PyObject? IPyObjectRecursiveRepr.RecursiveRepr(HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(this, _set, "{", "}", ids);
    }
}

// TODO: type