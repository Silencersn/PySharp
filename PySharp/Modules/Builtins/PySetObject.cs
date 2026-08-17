using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Collections;

namespace PySharp.Modules.Builtins;

public partial class PySetObject : PyObject, IPyObjectRecursiveRepr, ISet<PyObject>
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

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        if (_set.Count is 0)
            return PyStrObject.FromString("set()");

        return Utils.CollectionRecursiveRepr(context, this, _set, "{", "}", ids);
    }

    public static PySetObject CreateSet(params IEnumerable<PyObject> items)
    {
        return [.. items];
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
        return GetEnumerator();
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

[PyType("set")]
public sealed partial class PySetObjectType : PyTypeObject<PySetObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl_1), nameof(NewImpl_2))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters()]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return new PySetObject();
    }

    [PyFunctionParameters("iterable", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        return PyUtils.IterableToSet(context, arguments[0]);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

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

    protected override PyResult Len(PyCallContext context, PySetObject self)
    {
        return PyIntObject.FromInteger(self.Count);
    }

    protected override PyResult Iter(PyCallContext context, PySetObject self)
    {
        return new PySetIteratorObject(self);
    }

    protected override PyResult Add(PyCallContext context, PySetObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Sub(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject and not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return self.PyDifference(context, [other]);
    }

    protected override PyResult And(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject and not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return self.PyIntersection(context, [other]);
    }

    protected override PyResult Xor(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject and not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return self.PySymmetricDifference(context, other);
    }

    protected override PyResult Or(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject and not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return self.PyUnion(context, [other]);
    }

    protected override PyResult ISub(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject and not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        var result = self.PyDifferenceUpdate(context, [other]);
        if (result.IsError)
            return result;

        return self;
    }

    protected override PyResult IAnd(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject and not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        var result = self.PyIntersectionUpdate(context, [other]);
        if (result.IsError)
            return result;

        return self;
    }

    protected override PyResult IXor(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject and not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        var result = self.PySymmetricDifferenceUpdate(context, other);
        if (result.IsError)
            return result;

        return self;
    }

    protected override PyResult IOr(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject and not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        var result = self.PyUpdate(context, [other]);
        if (result.IsError)
            return result;

        return self;
    }

    protected override PyResult Lt(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.IsProperSubsetOf(otherSet));
        if (other is PyFrozenSetObject otherFrozenSet)
            return PyBoolObject.FromBoolean(self.IsProperSubsetOf(otherFrozenSet));
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Le(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.IsSubsetOf(otherSet));
        if (other is PyFrozenSetObject otherFrozenSet)
            return PyBoolObject.FromBoolean(self.IsSubsetOf(otherFrozenSet));
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Gt(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.IsProperSupersetOf(otherSet));
        if (other is PyFrozenSetObject otherFrozenSet)
            return PyBoolObject.FromBoolean(self.IsProperSupersetOf(otherFrozenSet));
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Ge(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.IsSupersetOf(otherSet));
        if (other is PyFrozenSetObject otherFrozenSet)
            return PyBoolObject.FromBoolean(self.IsSupersetOf(otherFrozenSet));
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Eq(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.SetEquals(otherSet));
        if (other is PyFrozenSetObject otherFrozenSet)
            return PyBoolObject.FromBoolean(self.SetEquals(otherFrozenSet));
        return PyNotImplementedObject.NotImplemented;
    }

    [PyMethod("add")]
    [PyFunctionParameters("item", "/")]
    private static PyResult Add(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        self.PyAdd(arguments[0]);
        return PyNoneObject.None;
    }

    [PyMethod("clear")]
    [PyFunctionParameters()]
    private static PyResult Clear(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        self.PyClear();
        return PyNoneObject.None;
    }

    [PyMethod("copy")]
    [PyFunctionParameters()]
    private static PyResult Copy(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyCopy();
    }

    [PyMethod("difference")]
    [PyFunctionParameters("*others")]
    private static PyResult Difference(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyDifference(context, arguments.ExtraArgs);
    }

    [PyMethod("difference_update")]
    [PyFunctionParameters("*others")]
    private static PyResult DifferenceUpdate(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyDifferenceUpdate(context, arguments.ExtraArgs);
    }

    [PyMethod("discard")]
    [PyFunctionParameters("item", "/")]
    private static PyResult Discard(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyDiscard(arguments[0]);
    }

    [PyMethod("intersection")]
    [PyFunctionParameters("*others")]
    private static PyResult Intersection(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIntersection(context, arguments.ExtraArgs);
    }

    [PyMethod("intersection_update")]
    [PyFunctionParameters("*others")]
    private static PyResult IntersectionUpdate(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIntersectionUpdate(context, arguments.ExtraArgs);
    }

    [PyMethod("isdisjoint")]
    [PyFunctionParameters("other", "/")]
    private static PyResult IsDisjoint(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIsDisjoint(context, arguments[0]);
    }

    [PyMethod("issubset")]
    [PyFunctionParameters("other", "/")]
    private static PyResult IsSubset(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIsSubset(context, arguments[0]);
    }

    [PyMethod("issuperset")]
    [PyFunctionParameters("other", "/")]
    private static PyResult IsSuperset(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIsSuperset(context, arguments[0]);
    }

    [PyMethod("pop")]
    [PyFunctionParameters()]
    private static PyResult Pop(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyPop();
    }

    [PyMethod("remove")]
    [PyFunctionParameters("item", "/")]
    private static PyResult Remove(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyRemove(arguments[0]);
    }

    [PyMethod("symmetric_difference")]
    [PyFunctionParameters("other", "/")]
    private static PyResult SymmetricDifference(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PySymmetricDifference(context, arguments[0]);
    }

    [PyMethod("symmetric_difference_update")]
    [PyFunctionParameters("other", "/")]
    private static PyResult SymmetricDifferenceUpdate(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PySymmetricDifferenceUpdate(context, arguments[0]);
    }

    [PyMethod("union")]
    [PyFunctionParameters("*others")]
    private static PyResult Union(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyUnion(context, arguments.ExtraArgs);
    }

    [PyMethod("update")]
    [PyFunctionParameters("*others")]
    private static PyResult Update(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyUpdate(context, arguments.ExtraArgs);
    }
}
