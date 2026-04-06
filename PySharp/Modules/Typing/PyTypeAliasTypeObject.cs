using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Modules.Typing;

internal sealed class PyTypeAliasTypeObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyTypeAliasTypeObjectType.Shared;

    internal readonly string _name;

    internal PyTypeAliasTypeObject(string name)
    {
        _name = name;
    }
}


[PyType("TypeAliasType", Module = "typing")]
internal sealed partial class PyTypeAliasTypeObjectType : PyTypeObject<PyTypeAliasTypeObject>
{
    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyTypeAliasTypeObject self)
    {
        return PyStrObject.FromString(self._name);
    }
}