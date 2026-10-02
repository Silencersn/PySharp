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

    public override PyTypeObject DefaultPyType { get; }

    // which face the descriptor wears is fixed by the type injected at
    // construction — member_descriptor or getset_descriptor — mirroring
    // the generator family's IsCoroutine check on the injected type
    internal bool IsGetSet => PyType is PyGetSetDescriptorObjectType;

    internal PyMemberDescriptorObject(PyTypeObject descriptorType, PyTypeObject declaringType, string name, PyMemberGetter getter, PyMemberSetter? setter, PyMemberDeleter? deleter)
    {
        _pyType = descriptorType;
        DefaultPyType = descriptorType;
        _declaringType = declaringType;
        _name = name;
        _getter = getter;
        _setter = setter;
        _deleter = deleter;
    }

    // shared tp_descr_get machinery, driven by the two descriptor
    // types' thin slot shells
    internal PyResult DescriptorGet(PyCallContext context, PyObject instance)
    {
        if (instance is PyNoneObject)
            return this;

        if (!instance.PyType.IsSubclassOf(_declaringType))
            return PyResult.TypeError(null);

        return _getter(context, instance);
    }

    internal PyResult DescriptorSet(PyCallContext context, PyObject instance, PyObject value)
    {
        if (_setter is null)
            return NotWritable();

        if (!instance.PyType.IsSubclassOf(_declaringType))
            return PyResult.TypeError(null);

        return _setter(context, instance, value);
    }

    internal PyResult DescriptorDelete(PyCallContext context, PyObject instance)
    {
        if (_deleter is null)
            return NotWritable();

        if (!instance.PyType.IsSubclassOf(_declaringType))
            return PyResult.TypeError(null);

        return _deleter(context, instance);
    }

    // a getset without a setter names the attribute and its declaring
    // type (getset_set, Objects/descrobject.c); a READONLY PyMemberDef
    // keeps the bare PyMember_SetOne sentence
    private PyResult NotWritable()
        => IsGetSet
            ? PyResult.AttributeError(PySR.Runtime_Attribute_NotWritable, _name, _declaringType.TpName)
            : PyResult.AttributeError(PySR.Runtime_Member_ReadOnly);
}

[PyType("member_descriptor")]
public sealed partial class PyMemberDescriptorObjectType : PyTypeObject<PyMemberDescriptorObject>
{
    protected override PyResult Get(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject owner)
        => self.DescriptorGet(context, instance);

    protected override PyResult Set(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject value)
        => self.DescriptorSet(context, instance, value);

    protected override PyResult Delete(PyCallContext context, PyMemberDescriptorObject self, PyObject instance)
        => self.DescriptorDelete(context, instance);

    // member_repr (Objects/descrobject.c:59)
    protected override PyResult Repr(PyCallContext context, PyMemberDescriptorObject self)
        => PyStrObject.FromString($"<member '{self._name}' of '{self._declaringType.TpName}' objects>");
}

[PyType("getset_descriptor")]
public sealed partial class PyGetSetDescriptorObjectType : PyTypeObject<PyMemberDescriptorObject>
{
    protected override PyResult Get(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject owner)
        => self.DescriptorGet(context, instance);

    protected override PyResult Set(PyCallContext context, PyMemberDescriptorObject self, PyObject instance, PyObject value)
        => self.DescriptorSet(context, instance, value);

    protected override PyResult Delete(PyCallContext context, PyMemberDescriptorObject self, PyObject instance)
        => self.DescriptorDelete(context, instance);

    // getset_repr (Objects/descrobject.c:66)
    protected override PyResult Repr(PyCallContext context, PyMemberDescriptorObject self)
        => PyStrObject.FromString($"<attribute '{self._name}' of '{self._declaringType.TpName}' objects>");
}
