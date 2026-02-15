using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;

namespace PySharp.Modules.Builtins;

public sealed class PyMethodObject : PyObject
{
    internal readonly PyObject _functionObj;
    internal readonly PyObject _target;

    public override PyTypeObject DefaultPyType => PyMethodObjectType.Shared;

    internal PyMethodObject(PyObject functionObj, PyObject target)
    {
        _functionObj = functionObj;
        _target = target;
    }
}

public sealed class PyMethodObjectType : PyTypeObject<PyMethodObjectType, PyMethodObject>
{
    public override string Module => "builtins";
    public override string Name => "method";

    public PyMethodObjectType()
    {
        AppendMemberDescriptor("__func__", static (_, method) => method._functionObj);
        AppendMemberDescriptor("__self__", static (_, method) => method._target);
    }

    protected override PyResult Call(PyCallContext context, PyMethodObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self._functionObj.Call(context, [self._target, .. args], kwargs);
    }

    protected override PyResult GetAttr(PyCallContext context, PyMethodObject self, PyObject item)
    {
        return PyOperators.GetAttr(context, self._functionObj, item);
    }
}