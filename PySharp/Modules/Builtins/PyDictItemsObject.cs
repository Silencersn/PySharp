using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

public sealed class PyDictItemsObject : PyObject
{
    internal readonly PyDictObject _dict;
    public override PyTypeObject DefaultPyType { get; }

    private PyDictItemsObject(PyTypeObject type, PyDictObject dict)
    {
        DefaultPyType = type;
        _dict = dict;
    }

    internal static PyDictItemsObject Items(PyDictObject dict)
    {
        return new PyDictItemsObject(PyDictItemsObjectType.Shared, dict);
    }
    internal static PyDictItemsObject Keys(PyDictObject dict)
    {
        return new PyDictItemsObject(PyDictKeysObjectType.Shared, dict);
    }
    internal static PyDictItemsObject Values(PyDictObject dict)
    {
        return new PyDictItemsObject(PyDictValuesObjectType.Shared, dict);
    }
}

[PyType("dict_items")]
public sealed partial class PyDictItemsObjectType : PyTypeObject<PyDictItemsObject>
{
    static PyDictItemsObjectType()
    {
        // CPython add_operators: unhashable types carry __hash__ = None in
        // the type dict (read face) while tp_hash raises the TypeError
        Shared.PyAttributes[PySpecialNames.Hash] = PyNoneObject.None;
    }

    // CPython PyObject_HashNotImplemented
    protected override PyResult Hash(PyCallContext context, PyDictItemsObject self)
    {
        return PyResult.TypeError(PySR.Runtime_Object_Unhashable, self.PyType.Name);
    }

    protected override PyResult Repr(PyCallContext context, PyDictItemsObject self)
    {
        return ReprView(context, self, "dict_items");
    }

    internal static PyResult ReprView(PyCallContext context, PyDictItemsObject self, string label)
    {
        var listResult = PyUtils.IterableToList(context, self);
        if (listResult.IsError)
            return listResult;

        var repr = PySpecialMethods.Repr(context, listResult.Value);
        if (repr.IsError)
            return repr;

        return PyStrObject.FromString($"{label}({repr.Value.Value})");
    }

    protected override PyResult Iter(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.Items(self);
    }

    protected override PyResult Reversed(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.ReversedItems(self);
    }

    protected override PyResult Len(PyCallContext context, PyDictItemsObject self)
    {
        return PyIntObject.FromInteger(self._dict.Count);
    }

    protected override PyResult Contains(PyCallContext context, PyDictItemsObject self, PyObject item)
    {
        return ContainsItem(context, self, item);
    }

    protected override PyResult Eq(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return CompareView(context, self, other, PyOperatorTypes.Eq);
    }

    protected override PyResult Lt(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return CompareView(context, self, other, PyOperatorTypes.Lt);
    }

    protected override PyResult Le(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return CompareView(context, self, other, PyOperatorTypes.LtE);
    }

    protected override PyResult Gt(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return CompareView(context, self, other, PyOperatorTypes.Gt);
    }

    protected override PyResult Ge(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return CompareView(context, self, other, PyOperatorTypes.GtE);
    }

    // CPython dictitems_contains: only a 2-tuple can match; the pair key
    // is looked up in the source dict (unhashable keys raise) and the
    // stored value is compared with ==.
    internal static PyResult ContainsItem(PyCallContext context, PyDictItemsObject self, PyObject item)
    {
        if (item is not PyTupleObject pair || pair.Count is not 2)
            return PyBoolObject.False;

        var found = self._dict.GetItem(context, pair[0]);
        if (found.IsKeyError)
            return PyBoolObject.False;
        if (found.IsError)
            return found;

        var equal = PyOperators.Eq(context, found.Value, pair[1]);
        if (equal.IsError)
            return equal;

        var isTrue = PySpecialMethods.Bool(context, equal.Value);
        if (isTrue.IsError)
            return isTrue;

        return PyBoolObject.FromBoolean(isTrue.Value.BoolValue);
    }

    // CPython dictview_richcompare: keys/items views compare as sets
    // against sets and other set-like views; the length gate runs before
    // the containment scan, and NE is the negated EQ result.
    internal static PyResult CompareView(PyCallContext context, PyDictItemsObject self, PyObject other, PyOperatorTypes op)
    {
        if (!IsSetLike(other))
            return PyNotImplementedObject.NotImplemented;

        int selfSize = self._dict.Count;
        int otherSize = SetLikeSize(other);

        var gate = op switch
        {
            PyOperatorTypes.Eq or PyOperatorTypes.NotEq => selfSize == otherSize,
            PyOperatorTypes.Lt => selfSize < otherSize,
            PyOperatorTypes.LtE => selfSize <= otherSize,
            PyOperatorTypes.Gt => selfSize > otherSize,
            _ => selfSize >= otherSize,
        };

        if (gate)
        {
            var scan = op is PyOperatorTypes.Gt or PyOperatorTypes.GtE
                ? AllContainedIn(context, other, self)
                : AllContainedIn(context, self, other);
            if (scan.IsError)
                return scan;

            gate = ((PyBoolObject)scan.Value).BoolValue;
        }

        return PyBoolObject.FromBoolean(op is PyOperatorTypes.NotEq ? !gate : gate);
    }

    private static bool IsSetLike(PyObject obj)
    {
        return obj is PySetObject or PyFrozenSetObject
            || (obj is PyDictItemsObject view && view.DefaultPyType is PyDictKeysObjectType or PyDictItemsObjectType);
    }

    private static int SetLikeSize(PyObject obj) => obj switch
    {
        PySetObject set => set.Count,
        PyFrozenSetObject frozenSet => frozenSet.Count,
        PyDictItemsObject view => view._dict.Count,
        _ => 0,
    };

    // CPython all_contained_in: every element of `source` must be
    // contained in `target`; containment errors propagate.
    private static PyResult AllContainedIn(PyCallContext context, PyObject source, PyObject target)
    {
        foreach (var element in EnumerateElements(source))
        {
            var contained = ContainsIn(context, target, element);
            if (contained.IsError)
                return contained;

            if (!((PyBoolObject)contained.Value).BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    private static IEnumerable<PyObject> EnumerateElements(PyObject source)
    {
        if (source is PyDictItemsObject view)
        {
            var entries = view._dict.Entries.ToArray();
            foreach (var entry in entries)
            {
                if (view.DefaultPyType is PyDictItemsObjectType)
                    yield return PyTupleObject.CreateTuple(entry.Key, entry.Value);
                else
                    yield return entry.Key;
            }

            yield break;
        }

        foreach (var element in (IEnumerable<PyObject>)source)
            yield return element;
    }

    private static PyResult ContainsIn(PyCallContext context, PyObject target, PyObject element)
    {
        if (target is PySetObject set)
            return PyBoolObject.FromBoolean(set.Contains(element));
        if (target is PyFrozenSetObject frozenSet)
            return PyBoolObject.FromBoolean(frozenSet.Contains(element));

        var view = (PyDictItemsObject)target;
        if (view.DefaultPyType is PyDictKeysObjectType)
        {
            var found = view._dict.GetItem(context, element);
            if (found.IsKeyError)
                return PyBoolObject.False;
            if (found.IsError)
                return found;

            return PyBoolObject.True;
        }

        return ContainsItem(context, view, element);
    }
}

public sealed class PyDictItemIteratorObject : PyObject
{
    internal readonly PyDictItemsObject _items;
    internal int _index;
    internal readonly int _count;
    private readonly bool _reverse;

    public override PyTypeObject DefaultPyType { get; }

    private PyDictItemIteratorObject(PyTypeObject type, PyDictItemsObject items, bool reverse)
    {
        DefaultPyType = type;
        _items = items;
        _reverse = reverse;
        _index = reverse ? items._dict.Count : -1;
        _count = items._dict.Count;
    }

    internal PyResult Next()
    {
        if (_index is -2)
            return PyResult.StopIteration();

        if (_count != _items._dict.Count)
            _index = -3;

        if (_index is -3)
            return PyResult.RuntimeError("dictionary changed size during iteration");

        if (_reverse)
        {
            if (_index - 1 < 0)
            {
                _index = -2;
                return PyResult.StopIteration();
            }

            return MakeResult(_items._dict.Entries[--_index]);
        }

        if (_index + 1 >= _count)
        {
            _index = -2;
            return PyResult.StopIteration();
        }

        return MakeResult(_items._dict.Entries[++_index]);
    }

    private PyResult MakeResult(PyDictObject.Entry entry) => DefaultPyType switch
    {
        PyDictItemIteratorObjectType or PyDictReverseItemIteratorObjectType => PyTupleObject.CreateTuple(entry.Key, entry.Value),
        PyDictKeyIteratorObjectType or PyDictReverseKeyIteratorObjectType => entry.Key,
        PyDictValueIteratorObjectType or PyDictReverseValueIteratorObjectType => entry.Value,
        _ => throw new UnreachableException()
    };

    internal static PyDictItemIteratorObject Items(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictItemIteratorObjectType.Shared, items, reverse: false);
    }
    internal static PyDictItemIteratorObject Keys(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictKeyIteratorObjectType.Shared, items, reverse: false);
    }
    internal static PyDictItemIteratorObject Values(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictValueIteratorObjectType.Shared, items, reverse: false);
    }
    internal static PyDictItemIteratorObject ReversedItems(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictReverseItemIteratorObjectType.Shared, items, reverse: true);
    }
    internal static PyDictItemIteratorObject ReversedKeys(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictReverseKeyIteratorObjectType.Shared, items, reverse: true);
    }
    internal static PyDictItemIteratorObject ReversedValues(PyDictItemsObject items)
    {
        return new PyDictItemIteratorObject(PyDictReverseValueIteratorObjectType.Shared, items, reverse: true);
    }

}

[PyType("dict_itemiterator")]
public sealed partial class PyDictItemIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}

[PyType("dict_keys")]
public sealed partial class PyDictKeysObjectType : PyTypeObject<PyDictItemsObject>
{
    static PyDictKeysObjectType()
    {
        // CPython add_operators: unhashable types carry __hash__ = None in
        // the type dict (read face) while tp_hash raises the TypeError
        Shared.PyAttributes[PySpecialNames.Hash] = PyNoneObject.None;
    }

    // CPython PyObject_HashNotImplemented
    protected override PyResult Hash(PyCallContext context, PyDictItemsObject self)
    {
        return PyResult.TypeError(PySR.Runtime_Object_Unhashable, self.PyType.Name);
    }

    protected override PyResult Repr(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemsObjectType.ReprView(context, self, "dict_keys");
    }

    protected override PyResult Iter(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.Keys(self);
    }

    protected override PyResult Reversed(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.ReversedKeys(self);
    }

    protected override PyResult Len(PyCallContext context, PyDictItemsObject self)
    {
        return PyIntObject.FromInteger(self._dict.Count);
    }

    protected override PyResult Contains(PyCallContext context, PyDictItemsObject self, PyObject item)
    {
        // CPython dictkeys_contains: a direct lookup in the source dict,
        // so unhashable keys report the lookup error instead of a scan.
        var result = self._dict.GetItem(context, item);
        if (result.IsSuccessful)
            return PyBoolObject.True;

        if (result.IsKeyError)
            return PyBoolObject.False;

        return result;
    }

    protected override PyResult Eq(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return PyDictItemsObjectType.CompareView(context, self, other, PyOperatorTypes.Eq);
    }

    protected override PyResult Lt(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return PyDictItemsObjectType.CompareView(context, self, other, PyOperatorTypes.Lt);
    }

    protected override PyResult Le(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return PyDictItemsObjectType.CompareView(context, self, other, PyOperatorTypes.LtE);
    }

    protected override PyResult Gt(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return PyDictItemsObjectType.CompareView(context, self, other, PyOperatorTypes.Gt);
    }

    protected override PyResult Ge(PyCallContext context, PyDictItemsObject self, PyObject other)
    {
        return PyDictItemsObjectType.CompareView(context, self, other, PyOperatorTypes.GtE);
    }
}

[PyType("dict_keyiterator")]
public sealed partial class PyDictKeyIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}

[PyType("dict_values")]
public sealed partial class PyDictValuesObjectType : PyTypeObject<PyDictItemsObject>
{
    protected override PyResult Repr(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemsObjectType.ReprView(context, self, "dict_values");
    }

    protected override PyResult Iter(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.Values(self);
    }

    protected override PyResult Reversed(PyCallContext context, PyDictItemsObject self)
    {
        return PyDictItemIteratorObject.ReversedValues(self);
    }

    protected override PyResult Len(PyCallContext context, PyDictItemsObject self)
    {
        return PyIntObject.FromInteger(self._dict.Count);
    }
}

[PyType("dict_reverseitemiterator")]
public sealed partial class PyDictReverseItemIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}

[PyType("dict_reversekeyiterator")]
public sealed partial class PyDictReverseKeyIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}

[PyType("dict_reversevalueiterator")]
public sealed partial class PyDictReverseValueIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}

[PyType("dict_valueiterator")]
public sealed partial class PyDictValueIteratorObjectType : PyTypeObject<PyDictItemIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self;
    }
    protected override PyResult Next(PyCallContext context, PyDictItemIteratorObject self)
    {
        return self.Next();
    }
}
