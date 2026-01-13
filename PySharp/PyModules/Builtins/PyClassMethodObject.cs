using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Builtins;

public sealed class PyClassMethodObject : PyObject
{
    internal readonly PyObject _wrapped;

    public override PyTypeObject DefaultPyType => PyClassMethodObjectType.Shared;

    public PyClassMethodObject(PyObject wrapped)
    {
        _wrapped = wrapped;
    }
}

public sealed class PyClassMethodObjectType : PyTypeObject<PyClassMethodObjectType, PyClassMethodObject>
{
    public override string Module => "builtins";
    public override string Name => "classmethod";

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    public PyClassMethodObjectType()
    {
    }

    [PyFunctionArgsDef("wrapped", "/")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        return new PyClassMethodObject(arguments[0]);
    }

    protected override PyResult Get(PyCallContext context, PyClassMethodObject self, PyObject instance, PyObject owner)
    {
        var type = owner;
        if (type is PyNoneObject)
        {
            if (instance is PyNoneObject)
                return PyResult.RaiseTypeError("__get__(None, None) is invalid");

            type = instance.PyType;
        }

        return new PyMethodObject(self._wrapped, type);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }
}
