using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Modules.String.TemplateLib;

public sealed class PyTemplateObject : PyObject
{
    internal readonly PyTupleObject _strings;
    internal readonly PyTupleObject _interpolations;

    internal PyTemplateObject(PyTupleObject strings, PyTupleObject interpolations)
    {
        _strings = strings;
        _interpolations = interpolations;
    }
}

[PyType("Template", Module = "string.templatelib")]
public sealed partial class PyTemplateObjectType : PyTypeObject<PyTemplateObjectType, PyTemplateObject>
{
    [PyProperty("strings")]
    private static PyResult Get_Strings(PyCallContext context, PyTemplateObject self)
    {
        return self._strings;
    }
    [PyProperty("interpolations")]
    private static PyResult Get_Interpolations(PyCallContext context, PyTemplateObject self)
    {
        return self._interpolations;
    }
}