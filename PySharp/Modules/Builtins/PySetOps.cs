using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

// Algorithms shared by set and frozenset, transposed from CPython
// Objects/setobject.c. Every table lookup carries the operation's live
// context, so user __hash__/__eq__ run as ordinary PyResult calls; hash
// errors never enter the table itself because each element's hash is
// computed once at operation entry and passed in with the element.
internal static class PySetOps
{
    // Table-level operations carry either a table or the comparison error:
    // PyResult<T> cannot wrap a PySetTable (it is not a PyObject).
    internal readonly struct TableResult
    {
        public readonly PyExceptionObject? Error;
        public readonly PySetTable Table;

        public bool IsError => Error is not null;

        public TableResult(PySetTable table)
        {
            Table = table;
            Error = null;
        }

        public TableResult(PyResult.PyExceptionResult result)
        {
            Table = null!;
            Error = result.Exception;
        }

        public TableResult(PyExceptionObject? error)
        {
            Table = null!;
            Error = error;
            Debug.Assert(error is not null, "a table result must carry an error when no table is present");
        }

        public static implicit operator TableResult(PyExceptionObject? error) => new(error);

        public static implicit operator TableResult(PySetTable table) => new(table);

        public static implicit operator TableResult(PyResult.PyExceptionResult result) => new(result);
    }

    internal static PySetTable? SetTableOf(PyObject other)
    {
        return other switch
        {
            PySetObject set => set._table,
            PyFrozenSetObject frozen => frozen._table,
            _ => null,
        };
    }

    // CPython set_isdisjoint gates its table fast path on
    // PyAnySet_CheckExact: subclass instances iterate through their
    // (possibly overridden) __iter__ instead
    internal static PySetTable? ExactSetTableOf(PyObject other)
    {
        return other switch
        {
            PySetObject set when ReferenceEquals(set.PyType, PySetObjectType.Shared) => set._table,
            PyFrozenSetObject frozen when ReferenceEquals(frozen.PyType, PyFrozenSetObjectType.Shared) => frozen._table,
            _ => null,
        };
    }

    // CPython set_unhashable_type: only a TypeError from the failed element
    // hash is re-raised with the set wording; any other error propagates raw.
    // Used by the direct element faces (add/contains/remove/discard).
    internal static PyResult<PyIntObject> ElementHash(PyCallContext context, PyObject item)
    {
        return WrapElementHash(context, item, PySpecialMethods.Hash(context, item));
    }

    private static PyResult<PyIntObject> WrapElementHash(PyCallContext context, PyObject item, PyResult<PyIntObject> hash)
    {
        return hash.IsError
            ? PyUtils.WrapHashFailure(context, item, hash, "a set element").Of<PyIntObject>()
            : hash;
    }

    // CPython set_intersection's non-set iterable branch
    // (setobject.c) hashes inline without re-wrapping: errors from the
    // issubset/intersection iterable conversions propagate exactly as raised
    private static PyResult<PyIntObject> RawElementHash(PyCallContext context, PyObject item)
    {
        return PySpecialMethods.Hash(context, item);
    }

    // CPython set_contains_lock_held/set_remove_impl/set_discard_impl
    // (setobject.c): a set key (subclasses included, frozensets never fail
    // here) whose own hash failed with a TypeError retries once with the
    // frozenset hash — equal contents produce the same value. add has no
    // such fallback.
    internal static PyResult<PyIntObject> LookupElementHash(PyCallContext context, PyObject item)
    {
        var hash = PySpecialMethods.Hash(context, item);
        if (!hash.IsError)
            return hash;

        // the frozenset-algorithm retry engages only on a TypeError
        // (CPython PyErr_ExceptionMatches); any other error propagates raw
        if (item is PySetObject set && PyTypeErrorObjectType.Shared.IsInstance(hash.Exception))
            return PyIntObject.FromInteger(PyFrozenSetObjectType.FrozenSetHashCore(set._table));

        return WrapElementHash(context, item, hash);
    }

    // CPython set_update_internal: set/frozenset arguments merge with their
    // stored hashes (no hash callbacks); any other iterable hashes each
    // element as it streams in, errors propagating raw
    internal static PyResult Update(PyCallContext context, PySetTable target, PyObject other)
    {
        var otherTable = SetTableOf(other);
        if (otherTable is not null)
            return UpdateFromSet(context, target, otherTable);

        return UpdateFromIterable(context, target, other);
    }

    internal static PyResult UpdateFromSet(PyCallContext context, PySetTable target, PySetTable other)
    {
        // CPython set_merge_lock_held: a.update(a) and a.update(set()) do nothing
        if (ReferenceEquals(target, other) || other.Count is 0)
            return PyNoneObject.None;

        // empty target: direct entry transfer, no probing and no callbacks
        if (target.Count is 0)
        {
            target.ReplaceWith(other.Clone());
            return PyNoneObject.None;
        }

        foreach (var (key, hash) in other.LiveEntries)
        {
            var added = target.Add(context, key, hash);
            if (added.IsError)
                return added;
        }

        return PyNoneObject.None;
    }

    private static PyResult UpdateFromIterable(PyCallContext context, PySetTable target, PyObject iterable)
    {
        var iterator = PySpecialMethods.Iter(context, iterable);
        if (iterator.IsError)
            return iterator;

        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator.Value);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    return PyNoneObject.None;
                return item;
            }

            var hash = ElementHash(context, item.Value);
            if (hash.IsError)
                return hash;

            var added = target.Add(context, item.Value, (long)hash.Value.Value);
            if (added.IsError)
                return added;
        }
    }

    // CPython set_issubset: set argument fast-fails on size, then iterates
    // self probing other; a non-set iterable is hashed element by element
    // and probed against self
    internal static PyResult<PyBoolObject> IsSubset(PyCallContext context, PySetTable so, PySetTable other)
    {
        if (so.Count > other.Count)
            return PyBoolObject.False;

        foreach (var (key, hash) in so.LiveEntries)
        {
            var contains = other.Contains(context, key, hash);
            if (contains.IsError)
                return contains;
            if (!contains.Value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    internal static PyResult<PyBoolObject> IsSubsetOfIterable(PyCallContext context, PySetTable so, PyObject other)
    {
        var iterator = PySpecialMethods.Iter(context, other);
        if (iterator.IsError)
            return iterator.ExceptionResult;

        // CPython set_issubset with a non-set iterable builds the
        // intersection first, so repeated hits count once; the walk stops
        // as soon as the deduplicated matches reach self's size
        var matches = new PySetTable();
        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator.Value);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;
                return item.ExceptionResult;
            }

            var hash = RawElementHash(context, item.Value);
            if (hash.IsError)
                return hash.ExceptionResult;

            var contains = so.Contains(context, item.Value, (long)hash.Value.Value);
            if (contains.IsError)
                return contains;
            if (!contains.Value.BoolValue)
                continue;

            var added = matches.Add(context, item.Value, (long)hash.Value.Value);
            if (added.IsError)
                return added;
            if (matches.Count >= so.Count)
                break;
        }

        return PyBoolObject.FromBoolean(matches.Count >= so.Count);
    }

    // CPython set_issuperset: set arguments recurse as the swapped subset
    // check; other iterables hash each element and probe self
    internal static PyResult<PyBoolObject> IsSuperset(PyCallContext context, PySetTable so, PySetTable other)
    {
        return IsSubset(context, other, so);
    }

    internal static PyResult<PyBoolObject> IsSupersetOfIterable(PyCallContext context, PySetTable so, PyObject other)
    {
        var iterator = PySpecialMethods.Iter(context, other);
        if (iterator.IsError)
            return iterator.ExceptionResult;

        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator.Value);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;
                return item.ExceptionResult;
            }

            var hash = ElementHash(context, item.Value);
            if (hash.IsError)
                return hash.ExceptionResult;

            var contains = so.Contains(context, item.Value, (long)hash.Value.Value);
            if (contains.IsError)
                return contains;
            if (!contains.Value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    // CPython set_isdisjoint: iterate the smaller table (ties: the other
    // argument), probe the larger; a single hit means not disjoint
    internal static PyResult<PyBoolObject> IsDisjoint(PyCallContext context, PySetTable so, PySetTable other)
    {
        // CPython set_isdisjoint_impl: self-comparison settles on the size
        if (ReferenceEquals(so, other))
            return PyBoolObject.FromBoolean(so.Count is 0);

        PySetTable iterate, probe;
        if (other.Count > so.Count)
        {
            iterate = so;
            probe = other;
        }
        else
        {
            iterate = other;
            probe = so;
        }

        foreach (var (key, hash) in iterate.LiveEntries)
        {
            var contains = probe.Contains(context, key, hash);
            if (contains.IsError)
                return contains;
            if (contains.Value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    internal static PyResult<PyBoolObject> IsDisjointOfIterable(PyCallContext context, PySetTable so, PyObject other)
    {
        var iterator = PySpecialMethods.Iter(context, other);
        if (iterator.IsError)
            return iterator.ExceptionResult;

        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator.Value);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;
                return item.ExceptionResult;
            }

            var hash = ElementHash(context, item.Value);
            if (hash.IsError)
                return hash.ExceptionResult;

            var contains = so.Contains(context, item.Value, (long)hash.Value.Value);
            if (contains.IsError)
                return contains;
            if (contains.Value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    // CPython set_intersection core: iterate the smaller table (ties: the
    // other argument), probe the larger; matches keep their stored hashes
    internal static TableResult IntersectionTable(PyCallContext context, PySetTable so, PySetTable other)
    {
        var result = new PySetTable();
        if (ReferenceEquals(so, other))
            return so.Clone();

        PySetTable iterate, probe;
        if (other.Count > so.Count)
        {
            iterate = so;
            probe = other;
        }
        else
        {
            iterate = other;
            probe = so;
        }

        foreach (var (key, hash) in iterate.LiveEntries)
        {
            var contains = probe.Contains(context, key, hash);
            if (contains.IsError)
                return contains.Exception;
            if (!contains.Value.BoolValue)
                continue;

            var added = result.Add(context, key, hash);
            if (added.IsError)
                return added.ExceptionResult;
        }

        return result;
    }

    // CPython set_intersection with a non-set iterable: hash each element,
    // probe so, collect matches; stops once the result reaches so's size
    internal static TableResult IntersectionTableOfIterable(PyCallContext context, PySetTable so, PyObject other)
    {
        var result = new PySetTable();
        var iterator = PySpecialMethods.Iter(context, other);
        if (iterator.IsError)
            return iterator.ExceptionResult;

        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator.Value);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;
                return item.ExceptionResult;
            }

            var hash = RawElementHash(context, item.Value);
            if (hash.IsError)
                return hash.ExceptionResult;

            var contains = so.Contains(context, item.Value, (long)hash.Value.Value);
            if (contains.IsError)
                return contains.Exception;
            if (!contains.Value.BoolValue)
                continue;

            var added = result.Add(context, item.Value, (long)hash.Value.Value);
            if (added.IsError)
                return added.ExceptionResult;
            if (result.Count >= so.Count)
                break;
        }

        return result;
    }

    // CPython set_difference: iterate self probing other, keeping
    // non-matches with their stored hashes
    internal static TableResult DifferenceTable(PyCallContext context, PySetTable so, PySetTable other)
    {
        var result = new PySetTable();
        foreach (var (key, hash) in so.LiveEntries)
        {
            var contains = other.Contains(context, key, hash);
            if (contains.IsError)
                return contains.Exception;
            if (contains.Value.BoolValue)
                continue;

            var added = result.Add(context, key, hash);
            if (added.IsError)
                return added.ExceptionResult;
        }

        return result;
    }


    // CPython set_difference_update_internal: iterate other and discard
    // each element from so
    internal static PyResult DifferenceUpdate(PyCallContext context, PySetTable so, PyObject other)
    {
        var otherTable = SetTableOf(other);
        if (otherTable is null)
            return DifferenceUpdateOfIterable(context, so, other);

        // CPython: when other is far larger than so, intersect first so the
        // iteration walks only the elements that can actually match
        var iterate = otherTable;
        if ((otherTable.Count >> 3) > so.Count)
        {
            var intersection = IntersectionTable(context, so, otherTable);
            if (intersection.IsError)
                return PyResult.FromException(intersection.Error!);
            iterate = intersection.Table;
        }

        var removed = DifferenceUpdate(context, so, iterate);
        if (removed.IsError)
            return removed;

        return PyNoneObject.None;
    }

    internal static PyResult DifferenceUpdate(PyCallContext context, PySetTable so, PySetTable other)
    {
        foreach (var (key, hash) in other.LiveEntries)
        {
            var removed = so.Remove(context, key, hash);
            if (removed.IsError)
                return removed;
        }

        so.CompactIfSparse();
        return PyNoneObject.None;
    }

    internal static PyResult DifferenceUpdateOfIterable(PyCallContext context, PySetTable so, PyObject other)
    {
        var iterator = PySpecialMethods.Iter(context, other);
        if (iterator.IsError)
            return iterator;

        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator.Value);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;
                return item;
            }

            var hash = ElementHash(context, item.Value);
            if (hash.IsError)
                return hash;

            var removed = so.Remove(context, item.Value, (long)hash.Value.Value);
            if (removed.IsError)
                return removed;
        }

        so.CompactIfSparse();
        return PyNoneObject.None;
    }

    // CPython set_symmetric_difference_update_set: iterate other, discarding
    // each element from so and adding the ones so did not have
    internal static PyResult SymmetricDifferenceUpdate(PyCallContext context, PySetTable so, PySetTable other)
    {
        foreach (var (key, hash) in other.LiveEntries)
        {
            var removed = so.Remove(context, key, hash);
            if (removed.IsError)
                return removed;
            if (removed.Value.BoolValue)
                continue;

            var added = so.Add(context, key, hash);
            if (added.IsError)
                return added;
        }

        return PyNoneObject.None;
    }

}
