using PySharp.PyRuntime.Comparison;

namespace PySharp.PyModules.Builtins;

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

    public void PySort(Func<PyObject, PyObject>? key = null, bool reverse = false)
    {
        IEnumerable<PyObject> _sortedItems;

        if (key is null)
        {
            _sortedItems = reverse
                ? _list.OrderDescending(PyObjectComparer.Default)
                : _list.Order(PyObjectComparer.Default);
        }
        else
        {
            _sortedItems = reverse
                ? _list.OrderByDescending(key, PyObjectComparer.Default)
                : _list.OrderBy(key, PyObjectComparer.Default);
        }

        List<PyObject> newList = [.. _sortedItems];
        _list.Clear();
        _list.AddRange(newList);
    }

    public void PyReverse()
    {
        _list.Reverse();
    }

    public PyListObject PyCopy()
    {
        return new PyListObject(_list);
    }
}
