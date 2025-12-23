using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyMemberDescriptorObject : PyObject, IPyDescriptor
{
    internal readonly Func<PyCallContext, PyObject, PyObject, PyResult> _getter;
    internal readonly Func<PyCallContext, PyObject, PyObject, PyResult>? _setter;

    public override PyTypeObject DefaultPyType => PyMemberDescriptorObjectType.Shared;

    bool IPyDescriptor.SupportsGet => true;
    bool IPyDescriptor.SupportsSet => true;
    bool IPyDescriptor.SupportsDelete => false;

    internal PyMemberDescriptorObject(Func<PyCallContext, PyObject, PyObject, PyResult> getter, Func<PyCallContext, PyObject, PyObject, PyResult>? setter = null)
    {
        _getter = getter;
        _setter = setter;
    }
}

public sealed class PyMemberDescriptorObjectType : PyTypeObject<PyMemberDescriptorObjectType, PyMemberDescriptorObject>
{
    public override string Name => "member_descriptor";

    protected internal override PyResult Get(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;
        return self._getter(context, instance, owner);
    }

    protected internal override PyResult Set(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject value)
    {
        if (self._setter is null)
            return PyResult.RaiseAttributeError("readonly attribute");
        return self._setter(context, instance, value);
    }
}