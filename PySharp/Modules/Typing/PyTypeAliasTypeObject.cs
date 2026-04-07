using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

internal sealed class PyTypeAliasTypeObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyTypeAliasTypeObjectType.Shared;

    internal readonly PyStrObject _name;
    private readonly PyFunctionObject _valueFunc;
    private PyObject? _value;

    internal PyTypeAliasTypeObject(string name, PyFunctionObject valueFunc)
    {
        _name = PyStrObject.FromString(name);
        _valueFunc = valueFunc;
    }

    internal PyResult GetValue(PyCallContext context)
    {
        if (_value is not null)
            return _value;

        var result = _valueFunc.Call(context);
        if (result.IsSuccessful)
            _value = result.Value;
        return result;
    }
}


[PyType("TypeAliasType", Module = "typing")]
internal sealed partial class PyTypeAliasTypeObjectType : PyTypeObject<PyTypeAliasTypeObject>
{
    protected override PyResult Repr(PyCallContext context, PyTypeAliasTypeObject self)
    {
        return self._name;
    }

    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyTypeAliasTypeObject self)
    {
        return self._name;
    }

    [PyProperty(PySpecialNames.Value)]
    private static PyResult Get_Value(PyCallContext context, PyTypeAliasTypeObject self)
    {
        return self.GetValue(context);
    }
}