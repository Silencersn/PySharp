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
}
