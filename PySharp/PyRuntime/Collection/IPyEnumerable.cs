using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections;

namespace PySharp.PyRuntime.Collection;

public interface IPyEnumerable : IEnumerable<PyObject?>
{
    public PyExceptionObject? Exception { get; }
}


internal sealed class PyIterator : IPyEnumerable
{
    private readonly PyObject _iterator;
    private readonly PyCallContext _context;

    public PyIterator(PyObject iterator, PyCallContext context)
    {
        _iterator = iterator;
        _context = context;
    }

    public PyExceptionObject? Exception { get; private set; }

    public IEnumerator<PyObject?> GetEnumerator()
    {
        while (true)
        {
            var item = _iterator.Next(_context);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    yield break;

                Exception = item.Exception;
                yield return null;
                yield break;
            }

            yield return item.Value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
