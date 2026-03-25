using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyMemberDescriptorObject : PyObject
{
    internal readonly PyTypeObject _declaringType;
    internal readonly PyMemberGetter _getter;
    internal readonly PyMemberSetter? _setter;
    internal readonly PyMemberDeleter? _deleter;

    public override PyTypeObject DefaultPyType => PyMemberDescriptorObjectType.Shared;

    internal PyMemberDescriptorObject(PyTypeObject declaringType, PyMemberGetter getter, PyMemberSetter? setter, PyMemberDeleter? deleter)
    {
        _declaringType = declaringType;
        _getter = getter;
        _setter = setter;
        _deleter = deleter;
    }
}

[PyType("member_descriptor")]
public sealed partial class PyMemberDescriptorObjectType : PyTypeObject<PyMemberDescriptorObject>
{

    protected override PyResult Get(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;

        if (!instance.PyType.IsSubclassOf(self._declaringType))
            return PyResult.TypeError(null);

        return self._getter(context, instance);
    }

    protected override PyResult Set(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject value)
    {
        if (self._setter is null)
            return PyResult.AttributeError("readonly attribute");

        if (!instance.PyType.IsSubclassOf(self._declaringType))
            return PyResult.TypeError(null);

        return self._setter(context, instance, value);
    }

    protected override PyResult Delete(PyCallContext context, PyMemberDescriptorObject self, PyObject instance)
    {
        if (self._deleter is null)
            return PyResult.AttributeError("readonly attribute");

        if (!instance.PyType.IsSubclassOf(self._declaringType))
            return PyResult.TypeError(null);

        return self._deleter(context, instance);
    }
}
