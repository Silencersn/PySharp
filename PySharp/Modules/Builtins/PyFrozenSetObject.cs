using PySharp.Modules.Typing;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Collections;

namespace PySharp.Modules.Builtins;

public partial class PyFrozenSetObject : PyObject, IPyObjectRecursiveRepr, IReadOnlySet<PyObject>
{
    internal readonly PySetTable _table;

    // CPython caches the frozenset hash in so->hash after the first
    // computation; set_richcompare uses it to settle unequal pairs without
    // element equality
    internal long? _cachedHash;

    public override PyTypeObject DefaultPyType => PyFrozenSetObjectType.Shared;

    public int Count => _table.Count;

    public PyFrozenSetObject()
    {
        _table = new PySetTable();
    }

    internal PyFrozenSetObject(PySetTable table)
    {
        _table = table;
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        // CPython frozenset_repr shares set_repr's subclass rule: the type
        // name wraps the plain {…} content (not frozenset({…})).
        if (PyType is not PyFrozenSetObjectType)
        {
            return _table.Count is 0
                ? PyStrObject.FromString($"{PyType.TpName}()")
                : PyUtils.CollectionRecursiveRepr(context, this, _table.SnapshotKeys(), $"{PyType.TpName}({{", "})", ids);
        }

        if (_table.Count is 0)
            return PyStrObject.FromString("frozenset()");

        return PyUtils.CollectionRecursiveRepr(context, this, _table.SnapshotKeys(), "frozenset({", "})", ids);
    }

    // Compile-time constant folding and embedded-C# construction: elements
    // are value-typed, hashed via the non-context dependency context
    public static PyFrozenSetObject CreateFrozenSet(params IEnumerable<PyObject> items)
    {
        var set = new PyFrozenSetObject();
        foreach (var item in items)
        {
            var hash = PySetOps.ElementHash(PyCallContext.NonContextDependency, item);
            if (hash.IsError)
                throw new PyRuntimeException(hash.Exception);

            var added = set._table.Add(PyCallContext.NonContextDependency, item, (long)hash.Value.Value);
            if (added.IsError)
                throw new PyRuntimeException(added.Exception);
        }

        return set;
    }

    // The .NET read face has no execution context; by contract only
    // value-typed elements reach it — see PySetObject.
    private static bool UnwrapEntry(PyResult<PyBoolObject> result)
    {
        return result.IsError ? throw new PyRuntimeException(result.Exception) : result.Value.BoolValue;
    }

    public bool Contains(PyObject item)
    {
        return UnwrapEntry(PyContainsEntry(PyCallContext.NonContextDependency, item));
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

    private static PyFrozenSetObject CopyOf(IEnumerable<PyObject> items)
    {
        var temp = new PyFrozenSetObject();
        foreach (var item in items)
        {
            var hash = PySetOps.ElementHash(PyCallContext.NonContextDependency, item);
            if (hash.IsError)
                throw new PyRuntimeException(hash.Exception);

            var added = temp._table.Add(PyCallContext.NonContextDependency, item, (long)hash.Value.Value);
            if (added.IsError)
                throw new PyRuntimeException(added.Exception);
        }

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

[PyType("frozenset")]
public sealed partial class PyFrozenSetObjectType : PyTypeObject<PyFrozenSetObject>
{
    // CPython Py_GenericAlias (descrobject.c): containers register
    // __class_getitem__ so frozenset[int] and friends build a types.GenericAlias
    [PyClassMethod(PySpecialNames.ClassGetItem)]
    [PyFunctionParameters("*args")]
    private static PyResult ClassGetItem(PyCallContext context, PyTypeObject cls, PyArguments arguments)
        => PyGenericAliasObject.ClassGetItem(cls, arguments);

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
        // CPython frozenset_new: set/frozenset arguments merge their stored
        // hashes (no hash callbacks); other iterables hash each element as
        // the table streams in
        var source = arguments[0];
        if (PySetOps.SetTableOf(source) is { } sourceTable)
            return new PyFrozenSetObject(sourceTable.Clone());

        var table = new PySetTable();
        var update = PySetOps.Update(context, table, source);
        if (update.IsError)
            return update;

        return new PyFrozenSetObject(table);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython frozenset_new: keyword arguments are rejected unless the
        // subtype overrides __init__ (slot_tp_init replaces the null
        // frozenset tp_init); frozenset is immutable so tp_new consumes the
        // iterable and the excess-argument message names the dynamic type
        if (kwargs.Count is not 0 &&
            ReferenceEquals(cls.Slots.Init, PyObjectType.Shared.Slots.Init))
            return PyResult.TypeError(PySR.Runtime_FrozenSet_TakesNoKwargs);
        if (args.Count > 1)
            return PyResult.TypeError(PySR.Runtime_Set_ExpectedAtMostOne, cls.Name, args.Count);

        // CPython make_new_set: frozenset() of an exact frozenset returns the
        // instance itself, but only when constructing the exact base type —
        // any other type (subclasses included) copies the elements into a
        // fresh object of that type.
        if (args.Count is 1 &&
            kwargs.Count is 0 &&
            args[0] is PyFrozenSetObject frozenSet &&
            ReferenceEquals(frozenSet.PyType, PyFrozenSetObjectType.Shared) &&
            ReferenceEquals(cls, PyFrozenSetObjectType.Shared))
            return frozenSet;

        // CPython frozenset_new ignores kwds when a subtype __init__ will
        // consume them; by this point only the keyword-less overload runs
        var obj = _new.Call(context, args, new Dictionary<string, PyObject>());
        if (obj.IsError)
            return obj;

        var eq = PyComparer.Eq(context, cls, this);
        if (eq.IsError)
            return eq;

        if (!eq.Value.BoolValue)
            obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyFrozenSetObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Contains(PyCallContext context, PyFrozenSetObject self, PyObject item)
    {
        return self.PyContainsEntry(context, item);
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
        // CPython frozenset_hash caches the result in so->hash after the
        // first computation.
        if (self._cachedHash is { } cached)
            return PyIntObject.FromInteger(cached);

        var result = FrozenSetHashCore(self._table);
        self._cachedHash = result;
        return PyIntObject.FromInteger(result);
    }

    private static ulong ShuffleBits(ulong h)
    {
        return (h ^ 89869747UL ^ (h << 16)) * 3644798167UL;
    }

    // CPython frozenset_hash_impl (Objects/setobject.c): xor of the slots'
    // STORED hashes spread through _shuffle_bits, then folded with the
    // element count and a final mix — order-independent and free of user
    // code. CPython xors whole hash-table slots and cancels null/dummy
    // parity afterwards; iterating active entries only reaches the same
    // value. Shared with the set-key lookup fallback in
    // PySetOps.LookupElementHash.
    internal static long FrozenSetHashCore(PySetTable table)
    {
        ulong hash = 0;
        foreach (var (_, storedHash) in table.LiveEntries)
            hash ^= ShuffleBits(unchecked((ulong)storedHash));

        // Factor in the number of active entries.
        hash ^= (ulong)(long)(table.Count + 1) * 1927868237UL;
        // Disperse patterns arising in nested frozensets.
        hash ^= (hash >> 11) ^ (hash >> 25);
        hash = hash * 69069UL + 907133923UL;

        // -1 is reserved as an error code.
        if (hash is ulong.MaxValue)
            hash = 590923713UL;

        return unchecked((long)hash);
    }

    // stage-3 pilot of the C-form middle layer: the set operators move from
    // the type-safe virtual surface to the slot-shaped entries. Each entry
    // guards both operands and declines with NotImplemented, which makes it
    // order-agnostic — the same virtual method serves the forward slot,
    // the reflected slot (the dispatcher already swapped the operands),
    // and the original-order call of the CPython binary_op1 third step
    protected internal override PyResult NbSub(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not PyFrozenSetObject frozenSelf)
            return PyNotImplementedObject.NotImplemented;
        if (other is not PySetObject && other is not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return frozenSelf.PyDifference(context, [other]);
    }

    protected internal override PyResult NbAnd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not PyFrozenSetObject frozenSelf)
            return PyNotImplementedObject.NotImplemented;
        if (other is not PySetObject && other is not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return frozenSelf.PyIntersection(context, [other]);
    }

    protected internal override PyResult NbXor(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not PyFrozenSetObject frozenSelf)
            return PyNotImplementedObject.NotImplemented;
        if (other is not PySetObject && other is not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return frozenSelf.PySymmetricDifference(context, other);
    }

    protected internal override PyResult NbOr(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not PyFrozenSetObject frozenSelf)
            return PyNotImplementedObject.NotImplemented;
        if (other is not PySetObject && other is not PyFrozenSetObject)
            return PyNotImplementedObject.NotImplemented;

        return frozenSelf.PyUnion(context, [other]);
    }

    // the six-slot comparison surface collapses into the single richcompare
    // entry; the mirror pairing (Lt<->Gt, Le<->Ge) is skeleton knowledge,
    // not per-slot code
    protected internal override PyResult RichCompare(PyCallContext context, PyObject self, PyObject other, PyOperatorTypes op)
    {
        if (self is not PyFrozenSetObject frozenSelf)
            return PyNotImplementedObject.NotImplemented;

        var otherTable = PySetOps.SetTableOf(other);
        if (otherTable is null)
            return PyNotImplementedObject.NotImplemented;

        return op switch
        {
            PyOperatorTypes.Lt => frozenSelf.Count >= otherTable.Count
                ? PyBoolObject.False
                : PySetOps.IsSubset(context, frozenSelf._table, otherTable),
            PyOperatorTypes.LtE => PySetOps.IsSubset(context, frozenSelf._table, otherTable),
            PyOperatorTypes.Gt => frozenSelf.Count <= otherTable.Count
                ? PyBoolObject.False
                : PySetOps.IsSuperset(context, frozenSelf._table, otherTable),
            PyOperatorTypes.GtE => PySetOps.IsSuperset(context, frozenSelf._table, otherTable),
            PyOperatorTypes.Eq => EqCore(context, frozenSelf, other, otherTable),
            PyOperatorTypes.NotEq => NotEqCore(context, frozenSelf, other, otherTable),
            _ => PyNotImplementedObject.NotImplemented,
        };
    }

    private static PyResult EqCore(PyCallContext context, PyFrozenSetObject self, PyObject other, PySetTable otherTable)
    {
        if (self.Count != otherTable.Count)
            return PyBoolObject.False;

        // CPython set_richcompare Py_EQ: cached frozenset hashes that differ
        // settle the comparison without touching element equality
        if (self._cachedHash is { } leftHash &&
            other is PyFrozenSetObject { _cachedHash: { } rightHash } &&
            leftHash != rightHash)
            return PyBoolObject.False;

        return PySetOps.IsSubset(context, self._table, otherTable);
    }

    // object's DefaultNe face over the same-core Eq: negate the value,
    // pass error/NotImplemented through untouched
    private static PyResult NotEqCore(PyCallContext context, PyFrozenSetObject self, PyObject other, PySetTable otherTable)
    {
        var eq = EqCore(context, self, other, otherTable);
        if (eq.IsError || eq.IsNotImplemented)
            return eq;

        return PyOperators.Not(context, (PyBoolObject)eq.Value);
    }

    // rewires the operator and comparison slots off the generated bridges
    // and the reflected-slot synthesizer onto the C-form virtual entries
    // above. PostConstruct runs after FillSlots (which ends with the
    // synthesis), so both legacy wirings land first and are replaced here
    protected override void PostConstruct()
    {
        base.PostConstruct();

        // one shared delegate instance per operator keeps the slot field and
        // the wrapper's func identical (SlotInvariantsTests asserts this,
        // and TrySetWrappedSlot recognizes slots by that identity); the
        // wrappers also need the runtime type to be PyBinaryFunction — a
        // bare method group would bind as its natural Func type
        var number = Slots.Number!;
        PyBinaryFunction sub = NbSub;
        number.Sub = sub;
        number.RSub = sub;
        PyAttributes[PySpecialNames.Sub] = new PyWrapperDescriptorObject(sub);
        PyBinaryFunction and = NbAnd;
        number.And = and;
        number.RAnd = and;
        PyAttributes[PySpecialNames.And] = new PyWrapperDescriptorObject(and);
        PyBinaryFunction xor = NbXor;
        number.Xor = xor;
        number.RXor = xor;
        PyAttributes[PySpecialNames.Xor] = new PyWrapperDescriptorObject(xor);
        PyBinaryFunction or = NbOr;
        number.Or = or;
        number.ROr = or;
        PyAttributes[PySpecialNames.Or] = new PyWrapperDescriptorObject(or);
        // the reflected wrappers keep the synthesizer's guarded flip: a
        // direct x.__r*(y) call only runs when y carries this layout
        PyAttributes[PySpecialNames.RSub] = new PyWrapperDescriptorObject(ReflectedWrapper(NbSub));
        PyAttributes[PySpecialNames.RAnd] = new PyWrapperDescriptorObject(ReflectedWrapper(NbAnd));
        PyAttributes[PySpecialNames.RXor] = new PyWrapperDescriptorObject(ReflectedWrapper(NbXor));
        PyAttributes[PySpecialNames.ROr] = new PyWrapperDescriptorObject(ReflectedWrapper(NbOr));

        // the six-slot comparison wiring collapses onto the single
        // RichCompare slot over the C-form entry above; the dict wrappers
        // are fixed-op views over the shared delegate (a bare method group
        // would also bind as its natural Func type)
        PyRichCompareFunction richCompare = RichCompare;
        Slots.RichCompare = richCompare;
        FillComparisonWrapper(PySpecialNames.Lt, richCompare, PyOperatorTypes.Lt);
        FillComparisonWrapper(PySpecialNames.Le, richCompare, PyOperatorTypes.LtE);
        FillComparisonWrapper(PySpecialNames.Eq, richCompare, PyOperatorTypes.Eq);
        FillComparisonWrapper(PySpecialNames.Ne, richCompare, PyOperatorTypes.NotEq);
        FillComparisonWrapper(PySpecialNames.Gt, richCompare, PyOperatorTypes.Gt);
        FillComparisonWrapper(PySpecialNames.Ge, richCompare, PyOperatorTypes.GtE);
    }

    private static PyBinaryFunction ReflectedWrapper(PyBinaryFunction entry)
        => (context, self, other) => other is PyFrozenSetObject
            ? entry(context, other, self)
            : PyNotImplementedObject.NotImplemented;

    [PyMethod("copy")]
    [PyFunctionParameters()]
    private static PyResult Copy(PyCallContext context, PyFrozenSetObject self, PyArguments arguments)
    {
        // CPython frozenset_copy: exact frozensets return themselves;
        // anything else copies into the base type
        if (ReferenceEquals(self.PyType, PyFrozenSetObjectType.Shared))
            return self;

        return new PyFrozenSetObject(self._table.Clone());
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
