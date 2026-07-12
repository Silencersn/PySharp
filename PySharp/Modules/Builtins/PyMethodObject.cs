using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

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

[PyType("method")]
public sealed partial class PyMethodObjectType : PyTypeObject<PyMethodObject>
{
    protected override PyResult Call(PyCallContext context, PyMethodObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self._functionObj.Call(context, [self._target, .. args], kwargs);
    }

    protected override PyResult GetAttr(PyCallContext context, PyMethodObject self, PyObject item)
    {
        return PyOperators.GetAttr(context, self._functionObj, item);
    }

    protected override PyResult Repr(PyCallContext context, PyMethodObject self)
    {
        // Try to get __qualname__ from the function
        var qualnameAttr = PyOperators.GetAttr(context, self._functionObj, PySpecialNames.QualName);
        string funcName;
        if (qualnameAttr.IsSuccessful && qualnameAttr.Value is PyStrObject qualnameStr)
            funcName = qualnameStr.Value;
        else
            funcName = "?";

        // Try to get repr of target
        var targetRepr = PySpecialMethods.Repr(context, self._target);
        string targetStr;
        if (targetRepr.IsSuccessful)
            targetStr = targetRepr.Value.Value;
        else
            targetStr = "?";

        return PyStrObject.FromString($"<bound method {funcName} of {targetStr}>");
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError("method() does not accept keyword arguments");

        if (args.Count is not 2)
            return PyResult.TypeError($"method() takes exactly 2 arguments ({args.Count} given)");

        var function = args[0];
        var instance = args[1];

        // Validate callable
        var callAttr = PyOperators.GetAttr(context, function, PySpecialNames.Interned.Call);
        if (callAttr.IsError && !callAttr.IsAttributeError)
            return callAttr;
        if (callAttr.IsAttributeError)
            return PyResult.TypeError(PySR.Format(PySR.Runtime_Object_NonCallable, function.PyType.FullName));

        // Validate instance is not None (CPython allows None but creates unbound method)
        if (instance is PyNoneObject)
            return PyResult.TypeError("instance must not be None");

        return new PyMethodObject(function, instance);
    }

    [PyProperty(PySpecialNames.Func)]
    private static PyResult Get_Func(PyCallContext context, PyMethodObject self)
    {
        return self._functionObj;
    }

    [PyProperty(PySpecialNames.Self)]
    private static PyResult Get_Self(PyCallContext context, PyMethodObject self)
    {
        return self._target;
    }

    [PyProperty(PySpecialNames.Doc)]
    private static PyResult Get_Doc(PyCallContext context, PyMethodObject self)
    {
        return PyOperators.GetAttr(context, self._functionObj, PySpecialNames.Doc);
    }
}
