using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyTupleObject
{
    [AIGenerated]
    public PyResult PyAdd(PyCallContext context, PyObject other)
    {
        if (other is not PyTupleObject otherTuple)
        {
            // The right operand's reflected __radd__ runs first (CPython's
            // tuple has no nb_add); the concat TypeError is the last resort
            // when it declines or does not exist.
            if (other.PyType.Slots.RAdd is not null)
            {
                var reflected = other.PyType.Slots.RAdd(context, other, this);
                if (!reflected.IsNotImplemented)
                    return reflected;
            }
            return PyResult.TypeError(PySR.Runtime_Tuple_AddNonTuple, other.PyType.QualName);
        }

        if (otherTuple.Count is 0)
            return this;
        if (Count is 0)
            return otherTuple;

        var newArray = new PyObject[Count + otherTuple.Count];
        Array.Copy(_array, 0, newArray, 0, Count);
        Array.Copy(otherTuple._array, 0, newArray, Count, otherTuple.Count);
        return new PyTupleObject(newArray);
    }

    [AIGenerated]
    public PyTupleObject PyMul(int n)
    {
        if (n <= 0 || Count is 0)
            return Empty;

        if (n is 1)
            return this;

        var newArray = new PyObject[Count * n];
        for (int i = 0; i < n; i++)
            Array.Copy(_array, 0, newArray, i * Count, Count);
        return new PyTupleObject(newArray);
    }

    [AIGenerated]
    public int PyIndex(PyCallContext context, PyObject item, int start, int end)
    {
        start = PyUtils.MapIndex(start, Count);
        end = PyUtils.MapIndex(end, Count);

        for (int i = int.Max(0, start); i < int.Min(end, Count); i++)
        {
            if (context.Comparer.Equals(_array[i], item))
                return i;
        }

        return -1;
    }

    [AIGenerated]
    public int PyIndex(PyCallContext context, PyObject item, int start)
    {
        return PyIndex(context, item, start, Count);
    }

    [AIGenerated]
    public int PyIndex(PyCallContext context, PyObject item)
    {
        return PyIndex(context, item, 0, Count);
    }

    [AIGenerated]
    public int PyCount(PyCallContext context, PyObject item)
    {
        var count = 0;
        foreach (var x in _array)
        {
            if (context.Comparer.Equals(x, item))
                count++;
        }
        return count;
    }

    [AIGenerated]
    internal PyResult PyHash(PyCallContext context)
    {
        // A nested tuple hashes its items, which recurses back into here once
        // per level, and none of those steps enters a Python frame: the frame
        // counters cannot bound how deep hash() goes, nor how deep a nested
        // tuple used as a dict key or held in a set goes. Probe the native
        // stack (CPython's tuplehash calls PyObject_Hash, which its C-level
        // recursion check bounds)
        if (PyRecursionGuard.ProbeNativeStack() is { } recursionError)
            return PyResult.FromException(recursionError);

        // Python's tuple hash implementation is more complex, but here's a reasonable version for PySharp.
        // We use a combination of element hashes.
        long hash = 0x345678;
        long multiplier = 1000003;
        foreach (var item in _array)
        {
            var h = PySpecialMethods.Hash(context, item);
            if (h.IsError)
                return h;

            hash = (hash ^ (long)h.Value.Value) * multiplier;
            multiplier += 82520L + (long)Count * 2;
        }

        hash += 97531;
        if (hash is -1)
            hash = -2;

        return PyIntObject.FromInteger((int)hash);
    }
}
