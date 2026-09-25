using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Collections;

namespace PySharp.Modules.Builtins;

public partial class PySetObject : PyObject, IPyObjectRecursiveRepr, ISet<PyObject>
{
    internal readonly PySetTable _table;

    public override PyTypeObject DefaultPyType => PySetObjectType.Shared;

    public int Count => _table.Count;

    bool ICollection<PyObject>.IsReadOnly => false;

    public PySetObject()
    {
        _table = new PySetTable();
    }

    internal PySetObject(PySetTable table)
    {
        _table = table;
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        // CPython set_repr: subclass instances always render TypeName(…),
        // with TypeName() when empty, to stay distinguishable from the
        // builtin {…} form.
        if (PyType is not PySetObjectType)
        {
            return _table.Count is 0
                ? PyStrObject.FromString($"{PyType.TpName}()")
                : PyUtils.CollectionRecursiveRepr(context, this, _table.SnapshotKeys(), $"{PyType.TpName}({{", "})", ids);
        }

        if (_table.Count is 0)
            return PyStrObject.FromString("set()");

        return PyUtils.CollectionRecursiveRepr(context, this, _table.SnapshotKeys(), "{", "}", ids);
    }

    // Runtime construction (BUILD_SET): element hashes run under the live
    // context and hash errors carry CPython's set wording
    public static PyResult<PySetObject> CreateSet(PyCallContext context, params IEnumerable<PyObject> items)
    {
        var set = new PySetObject();
        foreach (var item in items)
        {
            var added = set.PyAdd(context, item);
            if (added.IsError)
                return added.ExceptionResult;
        }

        return set;
    }

    // Compile-time constant folding: elements are value-typed literals, so
    // hashing under the non-context dependency context never runs Python code
    public static PySetObject CreateSet(params IEnumerable<PyObject> items)
    {
        var set = new PySetObject();
        foreach (var item in items)
        {
            var added = set.PyAdd(PyCallContext.NonContextDependency, item);
            if (added.IsError)
                throw new PyRuntimeException(added.Exception);
        }

        return set;
    }

    // The .NET collection face has no execution context. By contract it
    // only receives value-typed elements (compile-time constants,
    // embedded-C# usage); a user-defined element would run its callbacks
    // under a frame-less context and surface as a .NET exception.
    private static bool UnwrapEntry(PyResult<PyBoolObject> result)
    {
        return result.IsError ? throw new PyRuntimeException(result.Exception) : result.Value.BoolValue;
    }

    public bool Add(PyObject item)
    {
        return UnwrapEntry(PyAddEntry(PyCallContext.NonContextDependency, item));
    }

    void ICollection<PyObject>.Add(PyObject item)
    {
        _ = Add(item);
    }

    public bool Contains(PyObject item)
    {
        return UnwrapEntry(PyContainsEntry(PyCallContext.NonContextDependency, item));
    }

    public bool Remove(PyObject item)
    {
        return UnwrapEntry(PyRemoveEntry(PyCallContext.NonContextDependency, item));
    }

    public void Clear()
    {
        _table.Clear();
    }

    public void CopyTo(PyObject[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(arrayIndex, array.Length - _table.Count);

        foreach (var key in _table.Keys)
            array[arrayIndex++] = key;
    }

    public void UnionWith(IEnumerable<PyObject> other)
    {
        foreach (var item in other)
            UnwrapEntry(PyAddEntry(PyCallContext.NonContextDependency, item));
    }

    public void ExceptWith(IEnumerable<PyObject> other)
    {
        foreach (var item in other)
            UnwrapEntry(PyRemoveEntry(PyCallContext.NonContextDependency, item));
    }

    public void IntersectWith(IEnumerable<PyObject> other)
    {
        var temp = new PySetObject();
        foreach (var item in other)
            UnwrapEntry(temp.PyAddEntry(PyCallContext.NonContextDependency, item));

        var intersection = PySetOps.IntersectionTable(PyCallContext.NonContextDependency, _table, temp._table);
        if (intersection.IsError)
            throw new PyRuntimeException(intersection.Error!);

        _table.ReplaceWith(intersection.Table);
    }

    public bool IsSubsetOf(IEnumerable<PyObject> other)
    {
        var temp = CopyOf(other);
        return UnwrapEntry(PySetOps.IsSubset(PyCallContext.NonContextDependency, _table, temp._table));
    }

    public bool IsProperSubsetOf(IEnumerable<PyObject> other)
    {
        var temp = CopyOf(other);
        return _table.Count < temp._table.Count
            && UnwrapEntry(PySetOps.IsSubset(PyCallContext.NonContextDependency, _table, temp._table));
    }

    public bool IsSupersetOf(IEnumerable<PyObject> other)
    {
        var temp = CopyOf(other);
        return UnwrapEntry(PySetOps.IsSubset(PyCallContext.NonContextDependency, temp._table, _table));
    }

    public bool IsProperSupersetOf(IEnumerable<PyObject> other)
    {
        var temp = CopyOf(other);
        return _table.Count > temp._table.Count
            && UnwrapEntry(PySetOps.IsSubset(PyCallContext.NonContextDependency, temp._table, _table));
    }

    public bool Overlaps(IEnumerable<PyObject> other)
    {
        var temp = CopyOf(other);
        return !UnwrapEntry(PySetOps.IsDisjoint(PyCallContext.NonContextDependency, _table, temp._table));
    }

    public bool SetEquals(IEnumerable<PyObject> other)
    {
        var temp = CopyOf(other);
        return _table.Count == temp._table.Count
            && UnwrapEntry(PySetOps.IsSubset(PyCallContext.NonContextDependency, temp._table, _table));
    }

    public void SymmetricExceptWith(IEnumerable<PyObject> other)
    {
        var temp = CopyOf(other);
        var result = PySetOps.SymmetricDifferenceUpdate(PyCallContext.NonContextDependency, _table, temp._table);
        if (result.IsError)
            throw new PyRuntimeException(result.Exception);
    }

    private static PySetObject CopyOf(IEnumerable<PyObject> items)
    {
        var temp = new PySetObject();
        foreach (var item in items)
            UnwrapEntry(temp.PyAddEntry(PyCallContext.NonContextDependency, item));

        return temp;
    }

    IEnumerator<PyObject> IEnumerable<PyObject>.GetEnumerator()
    {
        foreach (var key in _table.Keys)
            yield return key;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<PyObject>)this).GetEnumerator();
    }
}

[PyType("set")]
public sealed partial class PySetObjectType : PyTypeObject<PySetObject>
{
    static PySetObjectType()
    {
        // CPython add_operators: unhashable types carry __hash__ = None in
        // the type dict (read face) while tp_hash raises the TypeError
        Shared.PyAttributes[PySpecialNames.Hash] = PyNoneObject.None;
    }

    // CPython PyObject_HashNotImplemented
    protected override PyResult Hash(PyCallContext context, PySetObject self)
    {
        return PyResult.TypeError(PySR.Runtime_Object_Unhashable, self.PyType.Name);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython set_new: allocate an empty set and ignore all arguments;
        // the iterable is consumed by set.__init__, which a subclass
        // __init__ replaces
        return new PySetObject { _pyType = cls };
    }

    protected override PyResult Init(PyCallContext context, PySetObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython set_init: no keyword arguments and at most one positional
        // iterable; the excess-argument message names the dynamic type
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Set_TakesNoKwargs);
        if (args.Count > 1)
            return PyResult.TypeError(PySR.Runtime_Set_ExpectedAtMostOne, self.PyType.Name, args.Count);

        self.Clear();
        if (args.Count is 1)
            return self.PyUpdate(context, [args[0]]);

        return PyNoneObject.None;
    }

    protected override PyResult Repr(PyCallContext context, PySetObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Contains(PyCallContext context, PySetObject self, PyObject item)
    {
        return self.PyContainsEntry(context, item);
    }

    protected override PyResult Len(PyCallContext context, PySetObject self)
    {
        return PyIntObject.FromInteger(self.Count);
    }

    protected override PyResult Iter(PyCallContext context, PySetObject self)
    {
        return new PySetIteratorObject(self);
    }

    // CPython has no nb_add for set at all (the union operator is __or__):
    // the placeholder Add slot must not synthesize a reflected __radd__
    protected override bool SynthesizeReflectedAdd => false;
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

    // CPython set_richcompare: the size gates run before any element
    // callback, then the subset/superset checks do the element work
    protected override PyResult Lt(PyCallContext context, PySetObject self, PyObject other)
    {
        var otherTable = PySetOps.SetTableOf(other);
        if (otherTable is null)
            return PyNotImplementedObject.NotImplemented;

        if (self.Count >= otherTable.Count)
            return PyBoolObject.False;

        return PySetOps.IsSubset(context, self._table, otherTable);
    }

    protected override PyResult Le(PyCallContext context, PySetObject self, PyObject other)
    {
        var otherTable = PySetOps.SetTableOf(other);
        if (otherTable is null)
            return PyNotImplementedObject.NotImplemented;

        return PySetOps.IsSubset(context, self._table, otherTable);
    }

    protected override PyResult Gt(PyCallContext context, PySetObject self, PyObject other)
    {
        var otherTable = PySetOps.SetTableOf(other);
        if (otherTable is null)
            return PyNotImplementedObject.NotImplemented;

        if (self.Count <= otherTable.Count)
            return PyBoolObject.False;

        return PySetOps.IsSuperset(context, self._table, otherTable);
    }

    protected override PyResult Ge(PyCallContext context, PySetObject self, PyObject other)
    {
        var otherTable = PySetOps.SetTableOf(other);
        if (otherTable is null)
            return PyNotImplementedObject.NotImplemented;

        return PySetOps.IsSuperset(context, self._table, otherTable);
    }

    protected override PyResult Eq(PyCallContext context, PySetObject self, PyObject other)
    {
        var otherTable = PySetOps.SetTableOf(other);
        if (otherTable is null)
            return PyNotImplementedObject.NotImplemented;

        if (self.Count != otherTable.Count)
            return PyBoolObject.False;

        return PySetOps.IsSubset(context, self._table, otherTable);
    }

    [PyMethod("add")]
    [PyFunctionParameters("item", "/")]
    private static PyResult Add(PyCallContext context, PySetObject self, PyArguments arguments)
    {
        return self.PyAdd(context, arguments[0]);
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
        return self.PyDiscard(context, arguments[0]);
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
        return self.PyRemove(context, arguments[0]);
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
