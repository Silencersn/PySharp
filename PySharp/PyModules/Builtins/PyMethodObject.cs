namespace PySharp.PyModules.Builtins;

public sealed class PyMethodObject : PyObject
{
    internal readonly PyFunctionObject _functionObj;
    internal readonly PyObject _target;

    public override PyTypeObject DefaultPyType => PyMethodObjectType.Shared;

    internal PyMethodObject(PyFunctionObject functionObj, PyObject target)
    {
        _functionObj = functionObj;
        _target = target;
    }

    protected internal override PyObject? CallImpl(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _functionObj.Call([_target, .. args], kwargs);
    }

    protected internal override PyObject? GetAttrImpl(string item)
    {
        return _functionObj.GetAttribute(item);
    }
}

public sealed class PyMethodObjectType : PyPrimitiveTypeObject<PyMethodObjectType, PyMethodObject>
{
    public override string Name => "method";

    public PyMethodObjectType()
    {
        AppendMemberDescriptor<PyMethodObject>("__func__", method => method._functionObj);
        AppendMemberDescriptor<PyMethodObject>("__self__", method => method._target);
    }
}