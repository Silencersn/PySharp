using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyMemberDescriptorObject : PyObject
{
    internal readonly PyTypeObject _declaringType;
    internal readonly string _name;
    internal readonly PyMemberGetter _getter;
    internal readonly PyMemberSetter? _setter;
    internal readonly PyMemberDeleter? _deleter;

    // models tp_getset rather than a READONLY PyMemberDef, which decides
    // the message a missing setter/deleter reports
    internal readonly bool _isGetSet;

    public override PyTypeObject DefaultPyType => PyMemberDescriptorObjectType.Shared;

    internal PyMemberDescriptorObject(PyTypeObject declaringType, string name, PyMemberGetter getter, PyMemberSetter? setter, PyMemberDeleter? deleter, bool isGetSet = false)
    {
        _declaringType = declaringType;
        _name = name;
        _getter = getter;
        _setter = setter;
        _deleter = deleter;
        _isGetSet = isGetSet;
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
            return AttributeErrorNotWritable(self);

        if (!instance.PyType.IsSubclassOf(self._declaringType))
            return PyResult.TypeError(null);

        return self._setter(context, instance, value);
    }

    protected override PyResult Delete(PyCallContext context, PyMemberDescriptorObject self, PyObject instance)
    {
        if (self._deleter is null)
            return AttributeErrorNotWritable(self);

        if (!instance.PyType.IsSubclassOf(self._declaringType))
            return PyResult.TypeError(null);

        return self._deleter(context, instance);
    }

    // a getset without a setter names the attribute and its declaring
    // type (gset_set, Objects/descrobject.c); a READONLY PyMemberDef
    // keeps the bare PyMember_SetOne sentence
    private static PyResult AttributeErrorNotWritable(PyMemberDescriptorObject self)
        => self._isGetSet
            ? PyResult.AttributeError(PySR.Runtime_Attribute_NotWritable, self._name, self._declaringType.TpName)
            : PyResult.AttributeError(PySR.Runtime_Member_ReadOnly);
}
