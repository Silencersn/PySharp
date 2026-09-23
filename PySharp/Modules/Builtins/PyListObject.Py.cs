using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;

namespace PySharp.Modules.Builtins;

partial class PyListObject
{
    internal void PyAppend(PyObject item)
    {
        _list.Add(item);
    }

    internal void PyExtend(IEnumerable<PyObject> items)
    {
        _list.AddRange(items);
    }

    internal PyResult PyExtend(PyCallContext context, PyObject iterable)
    {
        // CPython list_extend's iterator path: the length hint is consulted
        // after starting iteration and its errors propagate
        var list = PyUtils.IteratorToListWithHint(context, iterable);
        if (list.IsError)
            return list;

        _list.AddRange(list.Value._list);
        return PyNoneObject.None;
    }

    internal void PyInsert(int index, PyObject item)
    {
        if (index < 0)
            index = int.Max(0, index + _list.Count);
        else
            index = int.Min(index, _list.Count);

        _list.Insert(index, item);
    }

    internal bool PyRemove(PyCallContext context, PyObject item)
    {
        for (int i = 0; i < _list.Count; i++)
        {
            if (context.Comparer.Equals(_list[i], item))
            {
                _list.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    internal PyObject PyPop(int index = -1)
    {
        index = PyUtils.MapIndex(index, _list.Count);
        var item = _list[index];
        _list.RemoveAt(index);
        return item;
    }

    internal int PyIndex(PyCallContext context, PyObject item, int start, int end)
    {
        start = PyUtils.MapIndex(start, _list.Count);
        end = PyUtils.MapIndex(end, _list.Count);
        // Clamp the search range to valid indices (CPython clamps an
        // out-of-range negative start to 0); matching PyTupleObject.PyIndex.
        for (int i = int.Max(0, start); i < int.Min(end, _list.Count); i++)
        {
            if (context.Comparer.Equals(_list[i], item))
                return i;
        }
        return -1;
    }

    internal int PyIndex(PyCallContext context, PyObject item, int start = 0)
    {
        return PyIndex(context, item, start, _list.Count);
    }

    internal int PyCount(PyCallContext context, PyObject item)
    {
        return _list.Count(listItem => context.Comparer.Equals(listItem, item));
    }

    internal PyResult PySort(PyCallContext context, PyObject keySelector, PyObject reverse)
    {
        var result = PySpecialMethods.Bool(context, reverse);
        if (result.IsError)
            return result;
        if (keySelector is PyNoneObject)
        {
            return PySort(context, reverse: result.Value.BoolValue);
        }
        else
        {
            Dictionary<PyObject, PyObject> itemToKey = [];
            foreach (var item in _list)
            {
                var key = keySelector.Call(context, [item]);
                if (key.IsError)
                    return key;
                itemToKey[item] = key.Value;
            }
            return PySort(context, item => itemToKey[item], result.Value.BoolValue);
        }
    }

    internal PyResult PySort(PyCallContext context, Func<PyObject, PyObject>? key = null, bool reverse = false)
    {
        // CPython 3.14 listsort (Objects/listobject.c): key results are
        // computed once over the original order, a reverse sort reverses
        // the arrays, sorts ascending, and reverses back, and every
        // comparison asks PyObject_RichCompareBool(pivot, placed, Py_LT) —
        // the pivot is the left operand — with the result interpreted by
        // truthiness. An asymmetric __lt__ makes the operand order
        // observable, so the algorithm's comparison pattern is ported
        // faithfully: for n < 64 listsort is exactly one count_run plus one
        // binarysort.
        var items = _list.ToArray();
        PyObject[]? keys = null;
        if (key is not null)
        {
            keys = new PyObject[items.Length];
            for (int i = 0; i < items.Length; i++)
                keys[i] = key(items[i]);
        }
        if (reverse)
        {
            Array.Reverse(items);
            if (keys is not null)
                Array.Reverse(keys);
        }

        // beyond one count_run + binarysort pass listsort merges runs;
        // a stable merge over the same ISLT primitive agrees for
        // consistent comparators and never rejects inconsistent ones
        var status = items.Length < 64
            ? SmallSort(context, keys ?? items, keys is null ? null : items)
            : MergeSort(context, keys ?? items, keys is null ? null : items, 0, items.Length);
        if (status.IsError)
            return status;

        if (reverse)
        {
            Array.Reverse(items);
            if (keys is not null)
                Array.Reverse(keys);
        }

        _list.Clear();
        _list.AddRange(items);
        return PyNoneObject.None;
    }

    // one count_run + binarysort pass, the exact n < 64 listsort path
    private static PyResult SmallSort(PyCallContext context, PyObject[] a, PyObject[]? v)
    {
        // CPython listsort returns immediately for fewer than 2 elements
        if (a.Length < 2)
            return PyNoneObject.None;

        var runResult = CountRun(context, a, v, a.Length, out int run);
        if (runResult.IsError)
            return runResult;
        if (run < a.Length)
        {
            var sorted = BinarySort(context, a, v, a.Length, run);
            if (sorted.IsError)
                return sorted;
        }
        return PyNoneObject.None;
    }

    // stable top-down merge over the same ISLT primitive as listsort's
    // merge pass: the right-run element is the left operand, and equal
    // elements keep the left run first
    private static PyResult MergeSort(PyCallContext context, PyObject[] a, PyObject[]? v, int lo, int hi)
    {
        if (hi - lo < 2)
            return PyNoneObject.None;

        int mid = (lo + hi) >> 1;
        var lower = MergeSort(context, a, v, lo, mid);
        if (lower.IsError)
            return lower;
        var upper = MergeSort(context, a, v, mid, hi);
        if (upper.IsError)
            return upper;

        int leftLen = mid - lo;
        var left = new PyObject[leftLen];
        var leftV = v is null ? null : new PyObject[leftLen];
        Array.Copy(a, lo, left, 0, leftLen);
        if (v is not null && leftV is not null)
            Array.Copy(v, lo, leftV, 0, leftLen);

        int i = 0, j = mid, k = lo;
        while (i < leftLen && j < hi)
        {
            var lt = PyComparer.Lt(context, a[j], left[i]);
            if (lt.IsError)
                return lt;
            if (lt.Value.BoolValue)
            {
                a[k] = a[j];
                if (v is not null)
                    v[k] = v[j];
                j++;
            }
            else
            {
                a[k] = left[i];
                if (v is not null)
                    v[k] = leftV![i];
                i++;
            }
            k++;
        }
        while (i < leftLen)
        {
            a[k] = left[i];
            if (v is not null)
                v[k] = leftV![i];
            i++;
            k++;
        }
        return PyNoneObject.None;
    }

    // CPython count_run: length of the monotone run at the slice start,
    // permuted in place to ascending; equal subruns inside a descending run
    // are reversed so the final reversal restores their original order
    private static PyResult CountRun(PyCallContext context, PyObject[] a, PyObject[]? v, int nremaining, out int run)
    {
        run = 0;
        int n;
        for (n = 1; n < nremaining; n++)
        {
            var lt = PyComparer.Lt(context, a[n], a[n - 1]);
            if (lt.IsError)
                return lt;
            if (lt.Value.BoolValue)
                break;
        }
        if (n == nremaining)
        {
            run = n;
            return PyNoneObject.None;
        }

        if (n > 1)
        {
            var lt = PyComparer.Lt(context, a[0], a[n - 1]);
            if (lt.IsError)
                return lt;
            if (lt.Value.BoolValue)
            {
                run = n;
                return PyNoneObject.None;
            }
            ReverseSlice(a, v, 0, n);
        }
        ++n;

        int neq = 0;
        for (; n < nremaining; n++)
        {
            var lt = PyComparer.Lt(context, a[n], a[n - 1]);
            if (lt.IsError)
                return lt;
            if (lt.Value.BoolValue)
            {
                if (neq is not 0)
                {
                    ++neq;
                    ReverseSlice(a, v, n - neq, neq);
                    neq = 0;
                }
            }
            else
            {
                var gt = PyComparer.Lt(context, a[n - 1], a[n]);
                if (gt.IsError)
                    return gt;
                if (gt.Value.BoolValue)
                    break;
                ++neq;
            }
        }
        if (neq is not 0)
        {
            ++neq;
            ReverseSlice(a, v, n - neq, neq);
        }
        ReverseSlice(a, v, 0, n);

        for (; n < nremaining; n++)
        {
            var lt = PyComparer.Lt(context, a[n], a[n - 1]);
            if (lt.IsError)
                return lt;
            if (lt.Value.BoolValue)
                break;
        }
        run = n;
        return PyNoneObject.None;
    }

    private static void ReverseSlice(PyObject[] a, PyObject[]? v, int start, int length)
    {
        Array.Reverse(a, start, length);
        if (v is not null)
            Array.Reverse(v, start, length);
    }

    // CPython binarysort: stable binary insertion sort; a[:ok] is already
    // sorted, each pivot's slot is found with the pivot as the left operand
    private static PyResult BinarySort(PyCallContext context, PyObject[] a, PyObject[]? v, int n, int ok)
    {
        if (ok is 0)
            ++ok;
        for (; ok < n; ok++)
        {
            int l = 0, r = ok;
            var pivot = a[ok];
            var vPivot = v is null ? null : v[ok];
            while (l < r)
            {
                int m = (l + r) >> 1;
                var lt = PyComparer.Lt(context, pivot, a[m]);
                if (lt.IsError)
                    return lt;
                if (lt.Value.BoolValue)
                    r = m;
                else
                    l = m + 1;
            }
            for (int m = ok; m > l; --m)
                a[m] = a[m - 1];
            a[l] = pivot;
            if (v is not null)
            {
                var vItem = vPivot!;
                for (int m = ok; m > l; --m)
                    v[m] = v[m - 1];
                v[l] = vItem;
            }
        }
        return PyNoneObject.None;
    }

    internal void PyReverse()
    {
        _list.Reverse();
    }

    internal PyListObject PyCopy()
    {
        return CreateList(_list);
    }

    internal PyResult PyAdd(PyCallContext context, PyObject other)
    {
        if (other is not PyListObject otherList)
        {
            // The right operand's reflected __radd__ runs first (CPython's
            // list has no nb_add); the concat TypeError is the last resort
            // when it declines or does not exist.
            if (other.PyType.Slots.RAdd is not null)
            {
                var reflected = other.PyType.Slots.RAdd(context, other, this);
                if (!reflected.IsNotImplemented)
                    return reflected;
            }
            return PyResult.TypeError(PySR.Runtime_List_AddNonList, other.PyType.QualName);
        }

        var newList = new List<PyObject>(_list.Count + otherList.Count);
        newList.AddRange(_list);
        newList.AddRange(otherList._list);
        return new PyListObject(newList);
    }

    internal PyListObject PyMul(int n)
    {
        if (n <= 0)
            return new PyListObject();

        var newList = new List<PyObject>(_list.Count * n);
        for (int i = 0; i < n; i++)
            newList.AddRange(_list);
        return new PyListObject(newList);
    }

    internal PyListObject PyIMul(int n)
    {
        if (n <= 0)
        {
            _list.Clear();
            return this;
        }

        if (n is 1)
            return this;

        var originalItems = _list.ToArray();
        for (int i = 1; i < n; i++)
            _list.AddRange(originalItems);

        return this;
    }

    internal PyResult PySetItem(PyCallContext context, PyObject key, PyObject value)
    {
        if (key is PySliceObject slice)
        {
            var indicesResult = slice.Indices(context, _list.Count, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, stop, step, sliceLength) = indices;
            var iterableResult = PyUtils.IterableToList(context, value);
            if (iterableResult.IsError)
                return iterableResult;

            var values = iterableResult.Value._list;

            if (step is not 1 && values.Count != sliceLength)
                return PyResult.ValueError(PySR.Runtime_Sequence_SliceStep_AssignWrongSize, sliceLength, values.Count);

            if (step is 1)
            {
                int lower = int.Min(start, stop);
                int upper = int.Max(start, stop);
                _list.RemoveRange(lower, upper - lower);
                _list.InsertRange(lower, values);
            }
            else
            {
                for (int i = 0, idx = start; i < sliceLength; i++, idx += step)
                    _list[idx] = values[i];
            }

            return PyNoneObject.None;
        }

        var indexResult = PySpecialMethods.Index(context, key);
        if (indexResult.IsError)
            return indexResult;

        if (!indexResult.Value.IsInt32)
            return PyResult.IndexError(PySR.Runtime_Index_CannotFitInt);

        int index = indexResult.Value.Int32Value;
        if (PyUtils.IsIndexOutOfRange(index, _list.Count))
            return PyResult.IndexError(PySR.Runtime_List_AssignmentIndexOutOfRange);

        _list[PyUtils.MapIndex(index, _list.Count)] = value;
        return PyNoneObject.None;
    }

    internal PyResult PyDelItem(PyCallContext context, PyObject key)
    {
        if (key is PySliceObject slice)
        {
            var indicesResult = slice.Indices(context, _list.Count, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, stop, step, sliceLength) = indices;
            if (step is 1)
            {
                int lower = int.Min(start, stop);
                int upper = int.Max(start, stop);
                _list.RemoveRange(lower, upper - lower);
            }
            else
            {
                if (sliceLength is 0)
                    return PyNoneObject.None;

                var indicesToDelete = new List<int>(sliceLength);
                for (int i = 0, idx = start; i < sliceLength; i++, idx += step)
                    indicesToDelete.Add(idx);
                indicesToDelete.Sort();
                for (int i = indicesToDelete.Count - 1; i >= 0; i--)
                    _list.RemoveAt(indicesToDelete[i]);
            }
            return PyNoneObject.None;
        }

        var indexResult = PySpecialMethods.Index(context, key);
        if (indexResult.IsError)
            return indexResult;

        if (!indexResult.Value.IsInt32)
            return PyResult.IndexError(PySR.Runtime_Index_CannotFitInt);

        int index = indexResult.Value.Int32Value;
        if (PyUtils.IsIndexOutOfRange(index, _list.Count))
            return PyResult.IndexError(PySR.Runtime_List_AssignmentIndexOutOfRange);

        _list.RemoveAt(PyUtils.MapIndex(index, _list.Count));
        return PyNoneObject.None;
    }
}
