using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

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

[PyType("method_descriptor")]
public sealed partial class PyMethodDescriptorObjectType : PyTypeObject<PyMethodDescriptorObject>
{

    protected override PyResult Get(PyCallContext context, PyMethodDescriptorObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;

        if (!self._declaringType.IsInstance(instance))
            return PyResult.TypeError(PySR.Runtime_Descriptor_ReceiveObjectOfWrongType, self._name, self._declaringType.FullName, instance.PyType.FullName);

        return PyBuiltinFunctionOrMethodObject.CreateBoundMethodFromUnbound(self._name, instance, instance.PyType, self._uncompoundedDelegate);
    }

    protected override PyResult Call(PyCallContext context, PyMethodDescriptorObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is 0)
            return PyResult.TypeError(PySR.Runtime_Descriptor_NeedsArg, self._name, self._declaringType.FullName);

        if (!self._declaringType.IsInstance(args[0]))
            return PyResult.TypeError(PySR.Runtime_Descriptor_ReceiveObjectOfWrongType, self._name, self._declaringType.FullName, args[0].PyType.FullName);

        return self.UnboundMethod.Call(context, args, kwargs);
    }
}
