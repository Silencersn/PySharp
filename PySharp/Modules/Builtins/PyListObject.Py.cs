using PySharp.Compilation.CodeAnalysis;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.Comparison;
using System.Collections.Frozen;

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

    public bool PyRemove(PyObject item)
    {
        return _list.Remove(item);
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

    public int PyIndex(PyObject item, int start, int end)
    {
        start = Utils.MapIndex(start, _list.Count);
        end = Utils.MapIndex(end, _list.Count);
        return _list.IndexOf(item, start, end - start);
    }

    public int PyIndex(PyObject item, int start)
    {
        return PyIndex(item, start, _list.Count);
    }

    public int PyIndex(PyObject item)
    {
        return PyIndex(item, 0);
    }

    public int PyCount(PyObject item)
    {
        return _list.Count(listItem => listItem.Equals(item));
    }

    internal PyResult PySort(PyCallContext context, PyObject keySelector, PyObject reverse)
    {
        var result = PySpecialMethods.Bool(context, reverse);
        if (result.IsError)
            return result;
        if (keySelector is PyNoneObject)
        {
            PySort(reverse: result.Value.BoolValue);
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
            PySort(item => itemToKey[item], result.Value.BoolValue);
        }
        return PyNoneObject.None;
    }

    public void PySort(Func<PyObject, PyObject>? key = null, bool reverse = false)
    {
        IEnumerable<PyObject> sortedItems;

        if (key is null)
        {
            sortedItems = reverse
                ? _list.OrderDescending(PyObjectComparer.Default)
                : _list.Order(PyObjectComparer.Default);
        }
        else
        {
            sortedItems = reverse
                ? _list.OrderByDescending(key, PyObjectComparer.Default)
                : _list.OrderBy(key, PyObjectComparer.Default);
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
}
