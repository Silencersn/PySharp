using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyMemberDescriptorObject : PyObject, IPyDescriptor
{
    internal readonly Func<PyObject, PyObject, PyObject?> _getter;
    internal readonly Func<PyObject, PyObject, PyObject?>? _setter;

    public override PyTypeObject DefaultPyType => PyMemberDescriptorObjectType.Shared;

    bool IPyDescriptor.SupportsGet => true;
    bool IPyDescriptor.SupportsSet => true;
    bool IPyDescriptor.SupportsDelete => false;

    internal PyMemberDescriptorObject(Func<PyObject, PyObject, PyObject?> getter, Func<PyObject, PyObject, PyObject?>? setter = null)
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
        var result = self._getter(instance, owner);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }

    protected internal override PyResult Set(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject value)
    {
        if (self._setter is null)
            return PyResult.RaiseAttributeError("readonly attribute");
        var result = self._setter(instance, value);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
}