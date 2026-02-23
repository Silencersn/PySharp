using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyClassMethodObject : PyObject
{
    internal readonly PyObject _wrapped;

    public override PyTypeObject DefaultPyType => PyClassMethodObjectType.Shared;

    public PyClassMethodObject(PyObject wrapped)
    {
        _wrapped = wrapped;
    }
}

[PyType("classmethod")]
public sealed partial class PyClassMethodObjectType : PyTypeObject<PyClassMethodObjectType, PyClassMethodObject>
{

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
                return PyResult.TypeError(PySR.Runtime_Descriptor_GetNoneNoneInvalid);

            type = instance.PyType;
        }

        return new PyMethodObject(self._wrapped, type);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }
}
