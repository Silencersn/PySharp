using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyCallIteratorObject : PyObject
{
    internal readonly PyObject _callable;
    internal readonly PyObject _sentinel;
    internal bool _exhausted;

    public override PyTypeObject DefaultPyType => PyCallIteratorObjectType.Shared;

    internal PyCallIteratorObject(PyObject callable, PyObject sentinel)
    {
        _callable = callable;
        _sentinel = sentinel;
    }
}

// not exposed as a builtin name, mirroring CPython
[PyType("callable_iterator")]
public sealed partial class PyCallIteratorObjectType : PyTypeObject<PyCallIteratorObject>
{
    internal static PyCallIteratorObject New(PyObject callable, PyObject sentinel)
    {
        return new PyCallIteratorObject(callable, sentinel);
    }

    protected override PyResult Iter(PyCallContext context, PyCallIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyCallIteratorObject self)
    {
        // CPython clears it_callable on exhaustion, so no further calls happen
        if (self._exhausted)
            return PyResult.StopIteration();

        var result = self._callable.Call(context);
        if (result.IsError)
        {
            // a StopIteration from the callable ends the iteration silently
            if (!result.IsStopIteration)
                return result;

            self._exhausted = true;
            return PyResult.StopIteration();
        }

        // the identity fast path mirrors PyObject_RichCompareBool, which
        // compares equal before __eq__ ever runs
        if (ReferenceEquals(self._sentinel, result.Value))
        {
            self._exhausted = true;
            return PyResult.StopIteration();
        }

        var eq = PyOperators.Eq(context, self._sentinel, result.Value);
        if (eq.IsError)
            return eq;

        var isTrue = PySpecialMethods.Bool(context, eq.Value);
        if (isTrue.IsError)
            return isTrue;

        if (isTrue.Value.BoolValue)
        {
            self._exhausted = true;
            return PyResult.StopIteration();
        }

        return result;
    }
}
