using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Collections;

namespace PySharp.Modules.Builtins;

public partial class PyFrozenSetObject : PyObject, IPyObjectRecursiveRepr, IReadOnlySet<PyObject>
{
    private readonly HashSet<PyObject> _set;

    public override PyTypeObject DefaultPyType => PyFrozenSetObjectType.Shared;

    public int Count => _set.Count;

    public PyFrozenSetObject()
    {
        _set = new HashSet<PyObject>(PyObjectComparer.Default);
    }
    public PyFrozenSetObject(IEnumerable<PyObject> set)
    {
        _set = new HashSet<PyObject>(set, PyObjectComparer.Default);
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        if (_set.Count is 0)
            return PyStrObject.FromString("frozenset()");

        return Utils.CollectionRecursiveRepr(context, this, _set, "frozenset({", "})", ids);
    }

    public static PyFrozenSetObject CreateFrozenSet(params IEnumerable<PyObject> items)
    {
        return new PyFrozenSetObject(items);
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

    public bool Contains(PyObject item)
    {
        return _set.Contains(item);
    }
}

[PyType("frozenset")]
public sealed partial class PyFrozenSetObjectType : PyTypeObject<PyFrozenSetObject>
{

    [PyExport(PySpecialNames.New, nameof(NewImpl_1), nameof(NewImpl_2))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters()]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return new PyFrozenSetObject();
    }

    [PyFunctionParameters("iterable", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (iterable is PyFrozenSetObject frozenSet)
            return frozenSet;

        var setResult = PyUtils.IterableToSet(context, iterable);
        if (setResult.IsError)
            return setResult;

        return new PyFrozenSetObject(setResult.Value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;

        var eq = PyComparer.Eq(context, cls, this);
        if (eq.IsError)
            return eq.ExceptionResult;

        if (!eq.Value.BoolValue)
            obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyFrozenSetObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Bool(PyCallContext context, PyFrozenSetObject self)
    {
        return PyBoolObject.FromBoolean(self.Count > 0);
    }

    protected override PyResult Contains(PyCallContext context, PyFrozenSetObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self.Contains(item));
    }

    protected override PyResult Len(PyCallContext context, PyFrozenSetObject self)
    {
        return PyIntObject.FromInteger(self.Count);
    }

    protected override PyResult Iter(PyCallContext context, PyFrozenSetObject self)
    {
        return new PySetIteratorObject(self);
    }

    protected override PyResult Hash(PyCallContext context, PyFrozenSetObject self)
    {
        int hash = 0;
        foreach (var item in self)
        {
            var itemHash = PySpecialMethods.Hash(context, item);
            if (itemHash.IsError)
                return itemHash;
            hash ^= unchecked(itemHash.Value.Value.GetHashCode() + (int)0x9e3779b9 + (hash << 6) + (hash >> 2));
        }
        return PyIntObject.FromInteger(hash);
    }

    protected override PyResult Sub(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is not PySetObject && other is not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return self.PyDifference(context, [other]);
    }

    protected override PyResult And(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is not PySetObject && other is not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return self.PyIntersection(context, [other]);
    }

    protected override PyResult Xor(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is not PySetObject && other is not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return self.PySymmetricDifference(context, other);
    }

    protected override PyResult Or(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is not PySetObject && other is not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return self.PyUnion(context, [other]);
    }

    protected override PyResult Lt(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.IsProperSubsetOf(otherSet));
        if (other is PyFrozenSetObject otherFrozen)
            return PyBoolObject.FromBoolean(self.IsProperSubsetOf(otherFrozen));
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Le(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.IsSubsetOf(otherSet));
        if (other is PyFrozenSetObject otherFrozen)
            return PyBoolObject.FromBoolean(self.IsSubsetOf(otherFrozen));
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Gt(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.IsProperSupersetOf(otherSet));
        if (other is PyFrozenSetObject otherFrozen)
            return PyBoolObject.FromBoolean(self.IsProperSupersetOf(otherFrozen));
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Ge(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.IsSupersetOf(otherSet));
        if (other is PyFrozenSetObject otherFrozen)
            return PyBoolObject.FromBoolean(self.IsSupersetOf(otherFrozen));
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Eq(PyCallContext context, PyFrozenSetObject self, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(self.SetEquals(otherSet));
        if (other is PyFrozenSetObject otherFrozen)
            return PyBoolObject.FromBoolean(self.SetEquals(otherFrozen));
        return PyNotImplementedObject.NotImplemented;
    }

    [PyMethod("copy")]
    [PyFunctionParameters()]
    private static PyResult Copy(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        return self.PyType == PyFrozenSetObjectType.Shared ? self : new PyFrozenSetObject(self);
    }

    [PyMethod("difference")]
    [PyFunctionParameters("*others")]
    private static PyResult Difference(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        return self.PyDifference(context, arguments.ExtraArgs);
    }

    [PyMethod("intersection")]
    [PyFunctionParameters("*others")]
    private static PyResult Intersection(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        return self.PyIntersection(context, arguments.ExtraArgs);
    }

    [PyMethod("isdisjoint")]
    [PyFunctionParameters("other", "/")]
    private static PyResult IsDisjoint(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        return self.PyIsDisjoint(context, arguments[0]);
    }

    [PyMethod("issubset")]
    [PyFunctionParameters("other", "/")]
    private static PyResult IsSubset(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        return self.PyIsSubset(context, arguments[0]);
    }

    [PyMethod("issuperset")]
    [PyFunctionParameters("other", "/")]
    private static PyResult IsSuperset(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        return self.PyIsSuperset(context, arguments[0]);
    }

    [PyMethod("symmetric_difference")]
    [PyFunctionParameters("other", "/")]
    private static PyResult SymmetricDifference(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        return self.PySymmetricDifference(context, arguments[0]);
    }

    [PyMethod("union")]
    [PyFunctionParameters("*others")]
    private static PyResult Union(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        return self.PyUnion(context, arguments.ExtraArgs);
    }
}
