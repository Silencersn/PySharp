using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Modules.String.TemplateLib;

public sealed class PyInterpolationObject : PyObject
{
    internal readonly PyObject _value;
    internal readonly PyStrObject _expression;
    internal readonly PyStrObject? _conversion;
    internal readonly PyStrObject _formatSpec;

    public override PyTypeObject DefaultPyType => PyInterpolationObjectType.Shared;

    public PyInterpolationObject(PyObject value, PyStrObject expression, PyStrObject? conversion, PyStrObject formatSpec)
    {
        _value = value;
        _expression = expression;
        _conversion = conversion;
        _formatSpec = formatSpec;
    }
}

[PyType("Interpolation", Module = "string.templatelib")]
public sealed partial class PyInterpolationObjectType : PyTypeObject<PyInterpolationObject>
{
    protected override PyResult Repr(PyCallContext context, PyInterpolationObject self)
    {
        var valueRepr = PySpecialMethods.Repr(context, self._value);
        if (valueRepr.IsError)
            return valueRepr;

        return PyStrObject.FromString($"Interpolation({valueRepr.Value.Value}, {self._expression.Repr()}, {self._conversion?.Repr() ?? "None"}, {self._formatSpec.Repr()})");
    }

    [PyProperty("value")]
    private static PyResult Get_Value(PyCallContext context, PyInterpolationObject self)
    {
        return self._value;
    }
    [PyProperty("expression")]
    private static PyResult Get_Expression(PyCallContext context, PyInterpolationObject self)
    {
        return self._expression;
    }
    [PyProperty("conversion")]
    private static PyResult Get_Conversion(PyCallContext context, PyInterpolationObject self)
    {
        if (self._conversion is not null)
            return self._conversion;
        return PyNoneObject.None;
    }
    [PyProperty("format_spec")]
    private static PyResult Get_FormatSpec(PyCallContext context, PyInterpolationObject self)
    {
        return self._formatSpec;
    }
}
