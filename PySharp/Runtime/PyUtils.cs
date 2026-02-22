using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;

namespace PySharp.Runtime;

internal static class PyUtils
{
    private static PyResult<T> IterableToContainer<T>(PyCallContext context, PyObject iterable, Func<List<PyObject>, T> createContainer) where T : PyObject
    {
        var iterator = PySpecialMethods.Iter(context, iterable);
        if (iterator.IsError)
            return iterator.Of<T>();

        return IteratorToContainer(context, iterator.Value, createContainer);
    }

    private static PyResult<T> IteratorToContainer<T>(PyCallContext context, PyObject iterator, Func<List<PyObject>, T> createContainer) where T : PyObject
    {
        List<PyObject> list = [];

        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;

                return item.Of<T>();
            }

            list.Add(item.Value);
        }

        return createContainer(list);
    }

    public static PyResult<PyListObject> IterableToList(PyCallContext context, PyObject iterable)
    {
        return IterableToContainer(context, iterable, PyListObject.CreateProxy);
    }

    public static PyResult<PyTupleObject> IterableToTuple(PyCallContext context, PyObject iterable)
    {
        return IterableToContainer(context, iterable, PyTupleObject.CreateTuple);
    }

    public static PyResult<PyListObject> IteratorToList(PyCallContext context, PyObject iterator)
    {
        return IteratorToContainer(context, iterator, PyListObject.CreateProxy);
    }

    public static PyResult<PyTupleObject> IteratorToTuple(PyCallContext context, PyObject iterator)
    {
        return IteratorToContainer(context, iterator, PyTupleObject.CreateTuple);
    }

    public static PyResult<PyDictObject> MappingToDict(PyCallContext context, PyObject mapping, PyObject keysMethod)
    {
        var keys = keysMethod.Call(context);
        if (keys.IsError)
            return keys.Of<PyDictObject>();

        var keysList = IterableToList(context, keys.Value);
        if (keysList.IsError)
            return keysList.Of<PyDictObject>();

        PyDictObject dict = PyDictObject.CreateDict();

        foreach (var key in keysList.Value)
        {
            var value = PySpecialMethods.GetItem(context, mapping, key);
            if (value.IsError)
                return value.Of<PyDictObject>();

            dict.PySetItem(key, value.Value);
        }

        return dict;
    }

    public static PyResult<PyDictObject> IterableToDict(PyCallContext context, PyObject iterable)
    {
        var pairs = IterableToList(context, iterable);
        if (pairs.IsError)
            return pairs.Of<PyDictObject>();

        var dict = PyDictObject.CreateDict();

        for (int i = 0; i < pairs.Value.Count; i++)
        {
            var pairList = IterableToList(context, pairs.Value[i]);
            if (pairList.IsError)
                return pairList.Of<PyDictObject>();

            var count = pairList.Value.Count;
            if (count is not 2)
                return PyResult.ValueError(PySR.Runtime_Dictionary_UpdateEltLengthNotMatch, i, count).Of<PyDictObject>();

            var key = pairList.Value[0];
            var value = pairList.Value[1];
            dict.PySetItem(key, value);
        }

        return dict;
    }

    public static PyResult<PyDictObject> ToDict(PyCallContext context, PyObject iterableOrMapping)
    {
        if (iterableOrMapping is PyDictObject dict)
            return PyDictObject.CreateDict(dict);

        var keysMethod = PyOperators.GetAttr(context, iterableOrMapping, "keys");
        if (keysMethod.IsSuccessful)
            return MappingToDict(context, iterableOrMapping, keysMethod.Value);
        else if (keysMethod.IsAttributeError)
            return IterableToDict(context, iterableOrMapping);
        else
            return keysMethod.Of<PyDictObject>();
    }
}
