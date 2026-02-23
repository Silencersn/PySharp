using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyStaticMethodObject : PyObject
{
    internal readonly PyObject _wrapped;

    public override PyTypeObject DefaultPyType => PyStaticMethodObjectType.Shared;

    public PyStaticMethodObject(PyObject wrapped)
    {
        _wrapped = wrapped;
    }
}

[PyType("staticmethod")]
public sealed partial class PyStaticMethodObjectType : PyTypeObject<PyStaticMethodObjectType, PyStaticMethodObject>
{
    public override string Name => "staticmethod";

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    public PyStaticMethodObjectType()
    {
    }

    [PyFunctionArgsDef("wrapped", "/")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        return new PyStaticMethodObject(arguments[0]);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }

    protected override PyResult Get(PyCallContext context, PyStaticMethodObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject && owner is PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Descriptor_GetNoneNoneInvalid);

        return self._wrapped;
    }

    protected override PyResult Call(PyCallContext context, PyStaticMethodObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self._wrapped.Call(context, args, kwargs);
    }
}
