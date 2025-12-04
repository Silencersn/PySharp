using PySharp.PyRuntime;
using System.Text;

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

    public override PyBoolObject Bool()
    {
        return _set.Count > 0;
    }

    public override PyObject? Repr()
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(this);
    }

    PyObject? IPyObjectRecursiveRepr.RecursiveRepr(HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(this, _set, "{", "}", ids);
    }
}
