using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyEnumerateObject : PyObject
{
    internal readonly PyObject _iter;
    internal PyIntObject _index;

    public override PyTypeObject DefaultPyType => PyEnumerateObjectType.Shared;

    internal PyEnumerateObject(PyObject iter, PyIntObject start)
    {
        _iter = iter;
        _index = start;
    }
}

[PyType("enumerate")]
public sealed partial class PyEnumerateObjectType : PyTypeObject<PyEnumerateObjectType, PyEnumerateObject>
{
    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("iterable", "start=0")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var iter = PySpecialMethods.Iter(context, arguments[0]);
        if (iter.IsError)
            return iter;

        var start = PySpecialMethods.Index(context, arguments[1]);
        if (start.IsError)
            return start;

        return new PyEnumerateObject(iter.Value, start.Value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }

    protected override PyResult Iter(PyCallContext context, PyEnumerateObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyEnumerateObject self)
    {
        var result = PySpecialMethods.Next(context, self._iter);
        if (result.IsError)
            return result;

        var index = self._index;
        var tuple = PyTupleObject.CreateTuple(index, result.Value);
        self._index = PyIntObject.FromInteger(index.Value + 1);
        return tuple;
    }
}
