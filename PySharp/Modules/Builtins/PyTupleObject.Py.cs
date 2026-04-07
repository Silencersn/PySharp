using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;

namespace PySharp.Modules.Builtins;

partial class PyTupleObject
{
    [AIGenerated]
    public PyResult PyGetItem(PyCallContext context, PyObject item)
    {
        if (item is PySliceObject slice)
        {
            var (start, stop, step, sliceLength) = slice.Indices(_array.Length);
            if (sliceLength is 0)
                return Empty;

            var result = new PyObject[sliceLength];
            for (int i = 0, idx = start; i < sliceLength; i++, idx += step)
            {
                result[i] = _array[idx];
            }
            return new PyTupleObject(result);
        }

        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        return Utils.GetListItem(_array, indexResult.Value.Int32Value, PySR.Runtime_Tuple_IndexOutOfRange);
    }

    [AIGenerated]
    public PyResult PyAdd(PyObject other)
    {
        if (other is not PyTupleObject otherTuple)
            return PyResult.TypeError(PySR.Runtime_Tuple_AddNonTuple, other.PyType.FullName);

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
        {
            Array.Copy(_array, 0, newArray, i * Count, Count);
        }
        return new PyTupleObject(newArray);
    }

    [AIGenerated]
    public int PyIndex(PyObject item, int start, int end)
    {
        start = Utils.MapIndex(start, Count);
        end = Utils.MapIndex(end, Count);

        for (int i = int.Max(0, start); i < int.Min(end, Count); i++)
        {
            if (PyObjectComparer.Default.Equals(_array[i], item))
                return i;
        }

        return -1;
    }

    [AIGenerated]
    public int PyIndex(PyObject item, int start)
    {
        return PyIndex(item, start, Count);
    }

    [AIGenerated]
    public int PyIndex(PyObject item)
    {
        return PyIndex(item, 0, Count);
    }

    [AIGenerated]
    public int PyCount(PyObject item)
    {
        var count = 0;
        foreach (var x in _array)
        {
            if (PyObjectComparer.Default.Equals(x, item))
                count++;
        }
        return count;
    }

    [AIGenerated]
    internal PyResult PyHash(PyCallContext context)
    {
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
