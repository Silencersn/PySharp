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

[PyType("set")]
public sealed partial class PySetObjectType : PyTypeObject<PySetObject>
{
    [AIGenerated]
    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [AIGenerated]
    [PyFunctionArgsDef()]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return new PySetObject();
    }

    [AIGenerated]
    [PyFunctionArgsDef("iterable", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        return PyUtils.IterableToSet(context, arguments[0]);
    }

    [AIGenerated]
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    [AIGenerated]
    protected override PyResult Repr(PyCallContext context, PySetObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    [AIGenerated]
    protected override PyResult Bool(PyCallContext context, PySetObject self)
    {
        return PyBoolObject.FromBoolean(self.Count > 0);
    }

    [AIGenerated]
    protected override PyResult Contains(PyCallContext context, PySetObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self.Contains(item));
    }

    [AIGenerated]
    protected override PyResult Len(PyCallContext context, PySetObject self)
    {
        return PyIntObject.FromInteger(self.Count);
    }

    [AIGenerated]
    protected override PyResult Iter(PyCallContext context, PySetObject self)
    {
        // TODO: Create a specialized iterator for set
        return PyListObject.CreateProxy(self.ToList()).PyType.Slots.Iter!(context, PyListObject.CreateProxy(self.ToList()));
    }

    [AIGenerated]
    protected override PyResult Add(PyCallContext context, PySetObject self, PyObject other)
    {
        return Or(context, self, other);
    }

    [AIGenerated]
    protected override PyResult Sub(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return self.PyDifference(context, [otherSet]);
    }

    [AIGenerated]
    protected override PyResult And(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return self.PyIntersection(context, [otherSet]);
    }

    [AIGenerated]
    protected override PyResult Xor(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return self.PySymmetricDifference(context, otherSet);
    }

    [AIGenerated]
    protected override PyResult Or(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return self.PyUnion(context, [otherSet]);
    }

    [AIGenerated]
    protected override PyResult ISub(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        var result = self.PyDifferenceUpdate(context, [otherSet]);
        if (result.IsError)
            return result;

        return self;
    }

    [AIGenerated]
    protected override PyResult IAnd(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        var result = self.PyIntersectionUpdate(context, [otherSet]);
        if (result.IsError)
            return result;

        return self;
    }

    [AIGenerated]
    protected override PyResult IXor(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        var result = self.PySymmetricDifferenceUpdate(context, otherSet);
        if (result.IsError)
            return result;

        return self;
    }

    [AIGenerated]
    protected override PyResult IOr(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        var result = self.PyUpdate(context, [otherSet]);
        if (result.IsError)
            return result;

        return self;
    }

    [AIGenerated]
    protected override PyResult Lt(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(self.IsProperSubsetOf(otherSet));
    }

    [AIGenerated]
    protected override PyResult Le(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(self.IsSubsetOf(otherSet));
    }

    [AIGenerated]
    protected override PyResult Gt(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(self.IsProperSupersetOf(otherSet));
    }

    [AIGenerated]
    protected override PyResult Ge(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(self.IsSupersetOf(otherSet));
    }

    [AIGenerated]
    protected override PyResult Eq(PyCallContext context, PySetObject self, PyObject other)
    {
        if (other is not PySetObject otherSet)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(self.SetEquals(otherSet));
    }

    [AIGenerated]
    [PyMethod("add")]
    [PyFunctionArgsDef("item", "/")]
    private static PyResult Add(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        self.PyAdd(arguments[0]);
        return PyNoneObject.None;
    }

    [AIGenerated]
    [PyMethod("clear")]
    [PyFunctionArgsDef()]
    private static PyResult Clear(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        self.PyClear();
        return PyNoneObject.None;
    }

    [AIGenerated]
    [PyMethod("copy")]
    [PyFunctionArgsDef()]
    private static PyResult Copy(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyCopy();
    }

    [AIGenerated]
    [PyMethod("difference")]
    [PyFunctionArgsDef("*others")]
    private static PyResult Difference(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyDifference(context, arguments.ExtraArgs);
    }

    [AIGenerated]
    [PyMethod("difference_update")]
    [PyFunctionArgsDef("*others")]
    private static PyResult DifferenceUpdate(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyDifferenceUpdate(context, arguments.ExtraArgs);
    }

    [AIGenerated]
    [PyMethod("discard")]
    [PyFunctionArgsDef("item", "/")]
    private static PyResult Discard(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyDiscard(arguments[0]);
    }

    [AIGenerated]
    [PyMethod("intersection")]
    [PyFunctionArgsDef("*others")]
    private static PyResult Intersection(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIntersection(context, arguments.ExtraArgs);
    }

    [AIGenerated]
    [PyMethod("intersection_update")]
    [PyFunctionArgsDef("*others")]
    private static PyResult IntersectionUpdate(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIntersectionUpdate(context, arguments.ExtraArgs);
    }

    [AIGenerated]
    [PyMethod("isdisjoint")]
    [PyFunctionArgsDef("other", "/")]
    private static PyResult IsDisjoint(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIsDisjoint(context, arguments[0]);
    }

    [AIGenerated]
    [PyMethod("issubset")]
    [PyFunctionArgsDef("other", "/")]
    private static PyResult IsSubset(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIsSubset(context, arguments[0]);
    }

    [AIGenerated]
    [PyMethod("issuperset")]
    [PyFunctionArgsDef("other", "/")]
    private static PyResult IsSuperset(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyIsSuperset(context, arguments[0]);
    }

    [AIGenerated]
    [PyMethod("pop")]
    [PyFunctionArgsDef()]
    private static PyResult Pop(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyPop();
    }

    [AIGenerated]
    [PyMethod("remove")]
    [PyFunctionArgsDef("item", "/")]
    private static PyResult Remove(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyRemove(arguments[0]);
    }

    [AIGenerated]
    [PyMethod("symmetric_difference")]
    [PyFunctionArgsDef("other", "/")]
    private static PyResult SymmetricDifference(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PySymmetricDifference(context, arguments[0]);
    }

    [AIGenerated]
    [PyMethod("symmetric_difference_update")]
    [PyFunctionArgsDef("other", "/")]
    private static PyResult SymmetricDifferenceUpdate(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PySymmetricDifferenceUpdate(context, arguments[0]);
    }

    [AIGenerated]
    [PyMethod("union")]
    [PyFunctionArgsDef("*others")]
    private static PyResult Union(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyUnion(context, arguments.ExtraArgs);
    }

    [AIGenerated]
    [PyMethod("update")]
    [PyFunctionArgsDef("*others")]
    private static PyResult Update(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyUpdate(context, arguments.ExtraArgs);
    }
}
