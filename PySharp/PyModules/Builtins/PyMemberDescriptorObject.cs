using PySharp.PyRuntime;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyModules.Builtins;

public sealed class PyMemberDescriptorObject : PyObject
{
    private readonly Func<PyObject, PyObject?> _getter;

    public override PyTypeObject PyType => PyMemberDescriptorObjectType.Shared;

    internal PyMemberDescriptorObject(Func<PyObject, PyObject?> getter)
    {
        _getter = getter;
    }

    public override PyObject? Get(PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return this;

        return _getter(instance);
    }
}


public sealed class PyMemberDescriptorObjectType : PyPrimitiveTypeObject<PyMemberDescriptorObjectType, PyMemberDescriptorObject>
{
    public override string Name => "member_descriptor";
}