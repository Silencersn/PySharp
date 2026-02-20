using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using System.Collections;

namespace PySharp.Modules.Builtins;

public class PySetObject : PyObject, IPyObjectRecursiveRepr, ISet<PyObject>
{
    private readonly HashSet<PyObject> _set;

    public override PyTypeObject DefaultPyType => PySetObjectType.Shared;

    public int Count => _set.Count;

    bool ICollection<PyObject>.IsReadOnly => false;

    public PySetObject()
    {
        _set = new HashSet<PyObject>(PyObjectComparer.Default);
    }
    public PySetObject(IEnumerable<PyObject> set)
    {
        _set = new HashSet<PyObject>(set, PyObjectComparer.Default);
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        if (_set.Count is 0)
            return PyStrObject.FromString("set()");

        return Utils.CollectionRecursiveRepr(context, this, _set, "{", "}", ids);
    }

    public static PySetObject CreateSet(params IEnumerable<PyObject> items)
    {
        return new PySetObject(items);
    }

    public HashSet<PyObject>.Enumerator GetEnumerator()
    {
        return _set.GetEnumerator();
    }

    IEnumerator<PyObject> IEnumerable<PyObject>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_set).GetEnumerator();
    }

    public bool Add(PyObject item)
    {
        return _set.Add(item);
    }

    public void ExceptWith(IEnumerable<PyObject> other)
    {
        _set.ExceptWith(other);
    }

    public void IntersectWith(IEnumerable<PyObject> other)
    {
        _set.IntersectWith(other);
    }

    public bool IsProperSubsetOf(IEnumerable<PyObject> other)
    {
        return _set.IsProperSubsetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<PyObject> other)
    {
        return _set.IsProperSupersetOf(other);
    }

    public bool IsSubsetOf(IEnumerable<PyObject> other)
    {
        return _set.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<PyObject> other)
    {
        return _set.IsSupersetOf(other);
    }

    public bool Overlaps(IEnumerable<PyObject> other)
    {
        return _set.Overlaps(other);
    }

    public bool SetEquals(IEnumerable<PyObject> other)
    {
        return _set.SetEquals(other);
    }

    public void SymmetricExceptWith(IEnumerable<PyObject> other)
    {
        _set.SymmetricExceptWith(other);
    }

    public void UnionWith(IEnumerable<PyObject> other)
    {
        _set.UnionWith(other);
    }

    void ICollection<PyObject>.Add(PyObject item)
    {
        _set.Add(item);
    }

    public void Clear()
    {
        _set.Clear();
    }

    public bool Contains(PyObject item)
    {
        return _set.Contains(item);
    }

    public void CopyTo(PyObject[] array, int arrayIndex)
    {
        _set.CopyTo(array, arrayIndex);
    }

    public bool Remove(PyObject item)
    {
        return _set.Remove(item);
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
        return PyBoolObject.FromBoolean(self.Count > 0);
    }

    protected override PyResult Contains(PyCallContext context, PySetObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self.Contains(item));
    }
}
