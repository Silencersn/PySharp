using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

public sealed class PyMemberDescriptorObject : PyObject, IPyDescriptor
{
    private readonly Func<PyObject, PyObject?> _getter;
    private readonly Func<PyObject, PyObject, PyObject?>? _setter;

    public override PyTypeObject PyType => PyMemberDescriptorObjectType.Shared;

    bool IPyDescriptor.SupportsGet => true;

    bool IPyDescriptor.SupportsSet => true;

    bool IPyDescriptor.SupportsDelete => false;

    internal PyMemberDescriptorObject(Func<PyObject, PyObject?> getter, Func<PyObject, PyObject, PyObject?>? setter = null)
    {
        _getter = getter;
        _setter = setter;
    }

    public override PyObject? Get(PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return this;

        return _getter(instance);
    }

    public override PyObject? Set(PyObject instance, PyObject value)
    {
        if (_setter is null)
            return PyVirtualMachine.RaiseAttributeError("readonly attribute");

        return _setter(instance, value);
    }
}


public sealed class PyMemberDescriptorObjectType : PyPrimitiveTypeObject<PyMemberDescriptorObjectType, PyMemberDescriptorObject>
{
    public override string Name => "member_descriptor";
}