using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PySetObject
{
    // ---- element faces: one hash per operation, computed at entry ----

    internal PyResult<PyBoolObject> PyAddEntry(PyCallContext context, PyObject item)
    {
        var hash = PySetOps.ElementHash(context, item);
        if (hash.IsError)
            return hash.ExceptionResult;

        return _table.Add(context, item, (long)hash.Value.Value);
    }

    internal PyResult PyAdd(PyCallContext context, PyObject item)
    {
        var added = PyAddEntry(context, item);
        return added.IsError ? added.ExceptionResult : PyNoneObject.None;
    }

    internal PyResult<PyBoolObject> PyContainsEntry(PyCallContext context, PyObject item)
    {
        var hash = PySetOps.LookupElementHash(context, item);
        if (hash.IsError)
            return hash.ExceptionResult;

        return _table.Contains(context, item, (long)hash.Value.Value);
    }

    internal PyResult<PyBoolObject> PyRemoveEntry(PyCallContext context, PyObject item)
    {
        var hash = PySetOps.LookupElementHash(context, item);
        if (hash.IsError)
            return hash.ExceptionResult;

        return _table.Remove(context, item, (long)hash.Value.Value);
    }

    internal PyResult PyDiscard(PyCallContext context, PyObject item)
    {
        var removed = PyRemoveEntry(context, item);
        return removed.IsError ? removed.ExceptionResult : PyNoneObject.None;
    }

    internal PyResult PyRemove(PyCallContext context, PyObject item)
    {
        var removed = PyRemoveEntry(context, item);
        if (removed.IsError)
            return removed;
        if (!removed.Value.BoolValue)
            return PyResult.KeyError(item);

        return PyNoneObject.None;
    }

    internal PyResult<PyObject> PyPop()
    {
        // CPython set_pop: arbitrary element (a table slot here), no hashing
        var key = _table.PopFirst();
        if (key is null)
            return PyResult.KeyError(PyStrObject.FromString("pop from an empty set"));

        return key;
    }

    internal void PyClear()
    {
        _table.Clear();
    }

    internal PySetObject PyCopy()
    {
        // CPython make_new_set_basetype coerces subclass sources to the
        // base type, so copies (and every bulk-operation result) are plain
        return new PySetObject(_table.Clone());
    }

    // ---- bulk faces (CPython setobject.c algorithms, see PySetOps) ----

    internal PyResult PyUpdate(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        foreach (var other in others)
        {
            // CPython set_update_internal: a.update(a) does nothing
            if (ReferenceEquals(other, this))
                continue;

            var updated = PySetOps.Update(context, _table, other);
            if (updated.IsError)
                return updated;
        }

        return PyNoneObject.None;
    }

    // SET_UPDATE: update from a single iterable/set argument
    internal PyResult PyUpdateOne(PyCallContext context, PyObject other)
    {
        return PySetOps.Update(context, _table, other);
    }

    internal PyResult<PySetObject> PyUnion(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        // CPython set_union: copy self, then update from each other
        var newSet = PyCopy();
        foreach (var other in others)
        {
            if (ReferenceEquals(other, this))
                continue;

            var updated = PySetOps.Update(context, newSet._table, other);
            if (updated.IsError)
                return updated.ExceptionResult;
        }

        return newSet;
    }

    internal PyResult PyIntersectionUpdate(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        // CPython set_intersection_update_multi: fold self through the
        // others, then swap the final table in place
        var table = _table;
        foreach (var other in others)
        {
            var otherTable = PySetOps.SetTableOf(other);
            var intersection = otherTable is not null
                ? PySetOps.IntersectionTable(context, table, otherTable)
                : PySetOps.IntersectionTableOfIterable(context, table, other);
            if (intersection.IsError)
                return PyResult.FromException(intersection.Error!);

            table = intersection.Table;
        }

        if (!ReferenceEquals(table, _table))
            _table.ReplaceWith(table);

        return PyNoneObject.None;
    }

    internal PyResult<PySetObject> PyIntersection(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        // CPython set_intersection_multi: no others is a copy; otherwise the
        // result type follows self and the tables fold left to right
        if (others.Count is 0)
            return PyCopy();

        var result = _table;
        foreach (var other in others)
        {
            var otherTable = PySetOps.SetTableOf(other);
            var intersection = otherTable is not null
                ? PySetOps.IntersectionTable(context, result, otherTable)
                : PySetOps.IntersectionTableOfIterable(context, result, other);
            if (intersection.IsError)
                return PyResult.FromException(intersection.Error!).Of<PySetObject>();

            result = intersection.Table;
        }

        return new PySetObject(result);
    }

    internal PyResult PyDifferenceUpdate(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        foreach (var other in others)
        {
            // CPython set_difference_update_internal: a - a clears the set
            if (ReferenceEquals(other, this))
            {
                PyClear();
                continue;
            }

            var removed = PySetOps.DifferenceUpdate(context, _table, other);
            if (removed.IsError)
                return removed;
        }

        return PyNoneObject.None;
    }

    internal PyResult<PySetObject> PyDifference(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        // CPython set_difference_multi: no others is a copy; the first other
        // builds the result, the rest update it
        if (others.Count is 0)
            return PyCopy();

        var firstTable = PySetOps.SetTableOf(others[0]);
        PySetOps.TableResult table;
        if (firstTable is null || (_table.Count >> 2) > firstTable.Count)
        {
            // CPython set_copy_and_difference
            table = _table.Clone();
            var updated = PySetOps.DifferenceUpdate(context, table.Table, others[0]);
            if (updated.IsError)
                return updated.ExceptionResult;
        }
        else
        {
            table = PySetOps.DifferenceTable(context, _table, firstTable);
            if (table.IsError)
                return PyResult.FromException(table.Error!).Of<PySetObject>();
        }

        for (var i = 1; i < others.Count; i++)
        {
            var removed = PySetOps.DifferenceUpdate(context, table.Table, others[i]);
            if (removed.IsError)
                return removed.ExceptionResult;
        }

        return new PySetObject(table.Table);
    }

    internal PyResult PySymmetricDifferenceUpdate(PyCallContext context, PyObject other)
    {
        // CPython set_symmetric_difference_update: a ^= a clears the set;
        // non-set iterables are materialized into a temporary set first
        if (ReferenceEquals(other, this))
        {
            PyClear();
            return PyNoneObject.None;
        }

        var otherTable = PySetOps.SetTableOf(other);
        if (otherTable is null)
        {
            var temp = new PySetObject();
            var updated = PySetOps.Update(context, temp._table, other);
            if (updated.IsError)
                return updated;

            otherTable = temp._table;
        }

        var result = PySetOps.SymmetricDifferenceUpdate(context, _table, otherTable);
        if (result.IsError)
            return result;

        return PyNoneObject.None;
    }

    internal PyResult<PySetObject> PySymmetricDifference(PyCallContext context, PyObject other)
    {
        // CPython set_symmetric_difference: build result from other, then
        // symmetric-difference self's entries into it
        var result = new PySetTable();
        var update = PySetOps.Update(context, result, other);
        if (update.IsError)
            return update.ExceptionResult;

        var symmetric = PySetOps.SymmetricDifferenceUpdate(context, result, _table);
        if (symmetric.IsError)
            return symmetric.ExceptionResult;

        return new PySetObject(result);
    }

    internal PyResult<PyBoolObject> PyIsSubset(PyCallContext context, PyObject other)
    {
        var otherTable = PySetOps.SetTableOf(other);
        return otherTable is not null
            ? PySetOps.IsSubset(context, _table, otherTable)
            : PySetOps.IsSubsetOfIterable(context, _table, other);
    }

    internal PyResult<PyBoolObject> PyIsSuperset(PyCallContext context, PyObject other)
    {
        var otherTable = PySetOps.SetTableOf(other);
        return otherTable is not null
            ? PySetOps.IsSuperset(context, _table, otherTable)
            : PySetOps.IsSupersetOfIterable(context, _table, other);
    }

    internal PyResult<PyBoolObject> PyIsDisjoint(PyCallContext context, PyObject other)
    {
        var otherTable = PySetOps.ExactSetTableOf(other);
        return otherTable is not null
            ? PySetOps.IsDisjoint(context, _table, otherTable)
            : PySetOps.IsDisjointOfIterable(context, _table, other);
    }
}
