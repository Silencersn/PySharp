using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyReversedObject : PyObject
{
    internal readonly PyObject _seq;
    internal int _index;

    public override PyTypeObject DefaultPyType => PyReversedObjectType.Shared;

    internal PyReversedObject(PyObject seq, int len)
    {
        _seq = seq;
        _index = len - 1;
    }
}

[PyType("reversed")]
public sealed partial class PyReversedObjectType : PyTypeObject<PyReversedObjectType, PyReversedObject>
{
    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("object", "/")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var obj = arguments[0];

        var reversedFunc = obj.PyType.Slots.Reversed;
        if (reversedFunc is not null)
            return reversedFunc(context, obj);

        var lenFunc = obj.PyType.Slots.Len;
        var getItemFunc = obj.PyType.Slots.GetItem;
        if (lenFunc is null || getItemFunc is null)
            return PyResult.TypeError(PySR.Runtime_Builtin_Reversed_NonReversible, obj.PyType.FullName);

        var len = PySpecialMethods.Len(context, obj);
        if (len.IsError)
            return len;

        return new PyReversedObject(obj, len.Value.Int32Value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }

    protected override PyResult Iter(PyCallContext context, PyReversedObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyReversedObject self)
    {
        if (self._index < 0)
            return PyResult.StopIteration();

        var result = PySpecialMethods.GetItem(context, self._seq, PyIntObject.FromInteger(self._index));
        if (result.IsError)
        {
            self._index = -1;

            if (!PyIndexErrorObjectType.Shared.IsInstance(result.Exception))
                return result;

            return PyResult.StopIteration();
        }

        self._index--;
        return result;
    }
}
