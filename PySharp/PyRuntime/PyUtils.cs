using PySharp.CodeAnalysis;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime;

internal static class PyUtils
{
    public static PyResult<PyListObject> IterableToList(PyCallContext context, PyObject iterable)
    {
        var iterator = PySpecialMethods.Iter(context, iterable);
        if (iterator.IsError)
            return iterator.Of<PyListObject>();

        return IteratorToList(context, iterator.Value);
    }

    public static PyResult<PyListObject> IteratorToList(PyCallContext context, PyObject iterator)
    {
        List<PyObject> list = [];

        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;

                return item.Of<PyListObject>();
            }

            list.Add(item.Value);
        }

        return PyListObject.CreateProxy(list);
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

        foreach (var key in keysList.Value._list)
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

        for (int i = 0; i < pairs.Value._list.Count; i++)
        {
            var pairList = IterableToList(context, pairs.Value._list[i]);
            if (pairList.IsError)
                return pairList.Of<PyDictObject>();

            var count = pairList.Value._list.Count;
            if (count is not 2)
                return PyResult.ValueError(PySR.Runtime_Dictionary_UpdateEltLengthNotMatch, i, count).Of<PyDictObject>();

            var key = pairList.Value._list[0];
            var value = pairList.Value._list[1];
            dict.PySetItem(key, value);
        }

        return dict;
    }

    public static PyResult<PyDictObject> ToDict(PyCallContext context, PyObject iterableOrMapping)
    {
        var keysMethod = PyOperators.GetAttr(context, iterableOrMapping, "keys");
        if (keysMethod.IsSuccessful)
            return MappingToDict(context, iterableOrMapping, keysMethod.Value);
        else if (keysMethod.IsAttributeError)
            return IterableToDict(context, iterableOrMapping);
        else
            return keysMethod.Of<PyDictObject>();
    }
}
