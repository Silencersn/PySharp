using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyListObject
{
    public void PyAppend(PyObject item)
    {
        _list.Add(item);
    }

    public void PyExtend(IEnumerable<PyObject> items)
    {
        _list.AddRange(items);
    }

    public PyResult PyExtend(PyCallContext context, PyObject iterable)
    {
        var list = PyUtils.IterableToList(context, iterable);
        if (list.IsError)
            return list;

        _list.AddRange(list.Value._list);
        return PyNoneObject.None;
    }

    public void PyInsert(int index, PyObject item)
    {
        if (index < 0)
            index = int.Max(0, index + _list.Count);
        else
            index = int.Min(index, _list.Count);

        _list.Insert(index, item);
    }

    public bool PyRemove(PyCallContext context, PyObject item)
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

    public PyObject PyPop(int index = -1)
    {
        index = Utils.MapIndex(index, _list.Count);
        var item = _list[index];
        _list.RemoveAt(index);
        return item;
    }

    public void PyClear()
    {
        _list.Clear();
    }

    public int PyIndex(PyCallContext context, PyObject item, int start, int end)
    {
        start = Utils.MapIndex(start, _list.Count);
        end = Utils.MapIndex(end, _list.Count);
        // Clamp the search range to valid indices (CPython clamps an
        // out-of-range negative start to 0); matching PyTupleObject.PyIndex.
        for (int i = int.Max(0, start); i < int.Min(end, _list.Count); i++)
        {
            if (context.Comparer.Equals(_list[i], item))
                return i;
        }
        return -1;
    }

    public int PyIndex(PyCallContext context, PyObject item, int start)
    {
        return PyIndex(context, item, start, _list.Count);
    }

    public int PyIndex(PyCallContext context, PyObject item)
    {
        return PyIndex(context, item, 0);
    }

    public int PyCount(PyCallContext context, PyObject item)
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
            PySort(context, reverse: result.Value.BoolValue);
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
            PySort(context, item => itemToKey[item], result.Value.BoolValue);
        }
        return PyNoneObject.None;
    }

    public void PySort(PyCallContext context, Func<PyObject, PyObject>? key = null, bool reverse = false)
    {
        IEnumerable<PyObject> sortedItems;

        if (key is null)
        {
            sortedItems = reverse
                ? _list.OrderDescending(context.Comparer)
                : _list.Order(context.Comparer);
        }
        else
        {
            sortedItems = reverse
                ? _list.OrderByDescending(key, context.Comparer)
                : _list.OrderBy(key, context.Comparer);
        }

        var items = sortedItems.ToArray();
        _list.Clear();
        _list.AddRange(items);
    }

    public void PyReverse()
    {
        _list.Reverse();
    }

    public PyListObject PyCopy()
    {
        return CreateList(_list);
    }

    public PyResult PyAdd(PyObject other)
    {
        if (other is not PyListObject otherList)
            return PyNotImplementedObject.NotImplemented;

        var newList = new List<PyObject>(_list.Count + otherList.Count);
        newList.AddRange(_list);
        newList.AddRange(otherList._list);
        return new PyListObject(newList);
    }

    public PyListObject PyMul(int n)
    {
        if (n <= 0)
            return new PyListObject();

        var newList = new List<PyObject>(_list.Count * n);
        for (int i = 0; i < n; i++)
            newList.AddRange(_list);
        return new PyListObject(newList);
    }

    public PyListObject PyIMul(int n)
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

    public PyResult PyGetItem(PyCallContext context, PyObject item)
    {
        if (item is PySliceObject slice)
        {
            var indicesResult = slice.Indices(context, _list.Count, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, stop, step, sliceLength) = indices;
            var resultList = new List<PyObject>(sliceLength);
            for (int i = 0, idx = start; i < sliceLength; i++, idx += step)
                resultList.Add(_list[idx]);
            return new PyListObject(resultList);
        }

        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;
        if (!indexResult.Value.IsInt32)
            return PyResult.IndexError(PySR.Runtime_List_IndexOutOfRange);
        return Utils.GetListItem(_list, indexResult.Value.Int32Value, PySR.Runtime_List_IndexOutOfRange);
    }

    public PyResult PySetItem(PyCallContext context, PyObject key, PyObject value)
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

        if (!Utils.TrySetListItem(_list, indexResult.Value.Int32Value, value))
            return PyResult.IndexError(PySR.Runtime_List_IndexOutOfRange);

        return PyNoneObject.None;
    }

    public PyResult PyDelItem(PyCallContext context, PyObject key)
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

        int index = indexResult.Value.Int32Value;
        if (Utils.IsIndexOutOfRange(index, _list.Count))
            return PyResult.IndexError(PySR.Runtime_List_IndexOutOfRange);

        _list.RemoveAt(Utils.MapIndex(index, _list.Count));
        return PyNoneObject.None;
    }
}
