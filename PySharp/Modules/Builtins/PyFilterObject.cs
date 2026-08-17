using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyFilterObject : PyObject
{
    internal readonly PyObject _iter;
    internal readonly PyObject _func;

    public override PyTypeObject DefaultPyType => PyFilterObjectType.Shared;

    internal PyFilterObject(PyObject iter, PyObject func)
    {
        _iter = iter;
        _func = func;
    }
}

[PyType("filter")]
public sealed partial class PyFilterObjectType : PyTypeObject<PyFilterObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("function", "iterable", "/")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var func = arguments[0];
        var iter = PySpecialMethods.Iter(context, arguments[1]);
        if (iter.IsError)
            return iter;

        return new PyFilterObject(iter.Value, func);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }

    protected override PyResult Iter(PyCallContext context, PyFilterObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyFilterObject self)
    {
        while (true)
        {
            var item = PySpecialMethods.Next(context, self._iter);
            if (item.IsError)
                return item;

            PyObject condition;
            if (self._func is PyNoneObject)
            {
                condition = item.Value;
            }
            else
            {
                var conditionResult = self._func.Call(context, [item.Value]);
                if (conditionResult.IsError)
                    return conditionResult;
                condition = conditionResult.Value;
            }

            var boolResult = PySpecialMethods.Bool(context, condition);
            if (boolResult.IsError)
                return boolResult;

            if (boolResult.Value.BoolValue)
                return item.Value;
        }
    }
}
