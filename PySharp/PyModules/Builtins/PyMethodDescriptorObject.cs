using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyMethodDescriptorObject : PyObject
{
    internal readonly PyTypeObject _declaringType;
    internal readonly string _name;
    internal readonly PyUncompoundedDelegate _uncompoundedDelegate;

    internal PyBuiltinFunctionOrMethodObject UnboundMethod
    {
        get
        {
            return field ??= PyBuiltinFunctionOrMethodObject.CreateFunction(_name, _uncompoundedDelegate);
        }
    }

    public override PyTypeObject DefaultPyType => PyMethodDescriptorObjectType.Shared;

    internal PyMethodDescriptorObject(string name, PyTypeObject type, PyUncompoundedDelegate uncompoundedDelegate)
    {
        _declaringType = type;
        _name = name;
        _uncompoundedDelegate = uncompoundedDelegate;
    }
}

public sealed class PyMethodDescriptorObjectType : PyTypeObject<PyMethodDescriptorObjectType, PyMethodDescriptorObject>
{
    public override string Module => "builtins";
    public override string Name => "method_descriptor";

    protected override PyResult Get(PyCallContext context, PyMethodDescriptorObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;

        if (!self._declaringType.IsInstance(instance))
            return PyResult.RaiseTypeError($"descriptor '{self._name}' requires a '{self._declaringType.Name}' object but received a '{instance.PyType.Name}'");

        return PyBuiltinFunctionOrMethodObject.CreateBoundMethodFromUnbound(self._name, instance, instance.PyType, self._uncompoundedDelegate);
    }

    protected override PyResult Call(PyCallContext context, PyMethodDescriptorObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is 0)
            return PyResult.RaiseTypeError($"descriptor '{self._name}' of '{self._declaringType.Name}' object needs an argument");

        if (!self._declaringType.IsInstance(args[0]))
            return PyResult.RaiseTypeError($"descriptor '{self._name}' requires a '{self._declaringType.Name}' object but received a '{args[0].PyType.Name}'");

        return self.UnboundMethod.Call(context, args, kwargs);
    }
}