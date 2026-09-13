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
        // CPython frozenset_repr shares set_repr's subclass rule: the type
        // name wraps the plain {…} content (not frozenset({…})).
        if (PyType is not PyFrozenSetObjectType)
        {
            return _set.Count is 0
                ? PyStrObject.FromString($"{PyType.FullName}()")
                : PyUtils.CollectionRecursiveRepr(context, this, _set, $"{PyType.FullName}({{", "})", ids);
        }

        if (_set.Count is 0)
            return PyStrObject.FromString("frozenset()");

        return PyUtils.CollectionRecursiveRepr(context, this, _set, "frozenset({", "})", ids);
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
        // CPython frozenset_hash (Objects/setobject.c): xor of every element's
        // hash spread through _shuffle_bits, then folded with the element
        // count and a final mix, so the result is independent of order.
        // CPython xors whole hash-table slots and cancels null/dummy parity
        // afterwards; iterating active entries only reaches the same value.
        ulong hash = 0;
        foreach (var item in self)
        {
            var itemHash = PySpecialMethods.Hash(context, item);
            if (itemHash.IsError)
                return itemHash;
            hash ^= ShuffleBits(unchecked((ulong)(long)itemHash.Value.Value));
        }

        // Factor in the number of active entries.
        hash ^= (ulong)(long)(self.Count + 1) * 1927868237UL;
        // Disperse patterns arising in nested frozensets.
        hash ^= (hash >> 11) ^ (hash >> 25);
        hash = hash * 69069UL + 907133923UL;

        // -1 is reserved as an error code.
        if (hash is ulong.MaxValue)
            hash = 590923713UL;

        return PyIntObject.FromInteger(unchecked((long)hash));
    }

    private static ulong ShuffleBits(ulong h)
    {
        return ((h ^ 89869747UL) ^ (h << 16)) * 3644798167UL;
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
