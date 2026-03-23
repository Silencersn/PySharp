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

    public override PyTypeObject DefaultPyType => PyTemplateObjectType.Shared;

    internal PyTemplateObject(PyTupleObject strings, PyTupleObject interpolations)
    {
        _strings = strings;
        _interpolations = interpolations;
    }
}

[PyType("Template", Module = "string.templatelib")]
public sealed partial class PyTemplateObjectType : PyTypeObject<PyTemplateObjectType, PyTemplateObject>
{
    protected override PyResult Repr(PyCallContext context, PyTemplateObject self)
    {
        var stringsRepr = PySpecialMethods.Repr(context, self._strings);
        if (stringsRepr.IsError)
            return stringsRepr;

        var interpolationsRepr = PySpecialMethods.Repr(context, self._interpolations);
        if (interpolationsRepr.IsError)
            return interpolationsRepr;

        return PyStrObject.FromString($"Template(strings={stringsRepr.Value.Value}, interpolations={interpolationsRepr.Value.Value})");
    }

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