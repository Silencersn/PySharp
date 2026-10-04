using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

internal sealed class PyTypeAliasTypeObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyTypeAliasTypeObjectType.Shared;

    internal readonly PyStrObject _name;
    internal readonly PyFunctionObject _valueFunc;
    private PyObject? _value;

    // the alias's PEP 695 type params in declaration order; empty for a
    // non-generic alias (CPython stores NULL there and reports () instead)
    internal readonly PyTupleObject _typeParams;

    internal PyTypeAliasTypeObject(string name, PyFunctionObject valueFunc, PyTupleObject typeParams)
    {
        _name = PyStrObject.FromString(name);
        _valueFunc = valueFunc;
        _typeParams = typeParams;
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

    [PyProperty(PySpecialNames.TypeParams)]
    private static PyResult Get_TypeParams(PyCallContext context, PyTypeAliasTypeObject self)
    {
        return self._typeParams;
    }

    // typealias_parameters: without a TypeVarTuple there is nothing to
    // unpack, so __parameters__ is __type_params__ itself
    [PyProperty(PySpecialNames.Parameters)]
    private static PyResult Get_Parameters(PyCallContext context, PyTypeAliasTypeObject self)
    {
        return self._typeParams;
    }

    // CPython typealias_evaluate_value: the evaluator handed in at
    // construction is returned as-is for lazily-evaluated aliases
    [PyProperty("evaluate_value")]
    private static PyResult Get_EvaluateValue(PyCallContext context, PyTypeAliasTypeObject self)
    {
        return self._valueFunc;
    }

    // CPython typealias_subscript: the instance-level subscript protocol
    // creates a GenericAlias, and only a generic alias may be subscripted
    protected override PyResult GetItem(PyCallContext context, PyTypeAliasTypeObject self, PyObject key)
    {
        if (self._typeParams.Count is 0)
            return PyResult.TypeError(PySR.Runtime_TypeAlias_OnlyGenericSubscriptable);

        var args = key is PyTupleObject tuple ? tuple : PyTupleObject.CreateTuple([key]);
        return new PyGenericAliasObject(self, args);
    }
}
