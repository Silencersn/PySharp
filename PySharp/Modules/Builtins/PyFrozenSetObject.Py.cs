using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyFrozenSetObject
{
    internal PyResult<PyBoolObject> PyContainsEntry(PyCallContext context, PyObject item)
    {
        var hash = PySetOps.LookupElementHash(context, item);
        if (hash.IsError)
            return hash.ExceptionResult;

        return _table.Contains(context, item, (long)hash.Value.Value);
    }

    internal PyFrozenSetObject PyCopy()
    {
        // CPython make_new_set_basetype coerces subclass sources to the
        // base type, so copies (and every bulk-operation result) are plain
        return new PyFrozenSetObject(_table.Clone());
    }

    internal PyResult PyUnion(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        // CPython set_union: copy self, then update from each other
        var newSet = PyCopy();
        foreach (var other in others)
        {
            if (ReferenceEquals(other, this))
                continue;

            var updated = PySetOps.Update(context, newSet._table, other);
            if (updated.IsError)
                return updated;
        }

        return newSet;
    }

    internal PyResult PyIntersection(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
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
                return PyResult.FromException(intersection.Error!);

            result = intersection.Table;
        }

        return new PyFrozenSetObject(result);
    }

    internal PyResult PyDifference(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
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
                return updated;
        }
        else
        {
            table = PySetOps.DifferenceTable(context, _table, firstTable);
            if (table.IsError)
                return PyResult.FromException(table.Error!);
        }

        for (var i = 1; i < others.Count; i++)
        {
            var removed = PySetOps.DifferenceUpdate(context, table.Table, others[i]);
            if (removed.IsError)
                return removed;
        }

        return new PyFrozenSetObject(table.Table);
    }

    internal PyResult PySymmetricDifference(PyCallContext context, PyObject other)
    {
        // CPython set_symmetric_difference: build result from other, then
        // symmetric-difference self's entries into it; the result carries
        // self's dynamic type (frozenset)
        var result = new PySetTable();
        var update = PySetOps.Update(context, result, other);
        if (update.IsError)
            return update;

        var symmetric = PySetOps.SymmetricDifferenceUpdate(context, result, _table);
        if (symmetric.IsError)
            return symmetric;

        return new PyFrozenSetObject(result);
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
