using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

[PyType("coroutine")]
public sealed partial class PyCoroutineObjectType : PyTypeObject<PyGeneratorObject>
{
    protected override PyResult Repr(PyCallContext context, PyGeneratorObject self)
    {
        return PyStrObject.FromString($"<coroutine object {self.Name} at 0x{self.PyId:X16}>");
    }

    protected override PyResult Await(PyCallContext context, PyGeneratorObject self)
    {
        return self;
    }

    [PyMethod("send")]
    [PyFunctionParameters("value", "/")]
    private static PyResult Send(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return self.PyNext(context);

        return self.PySend(context, arguments[0]);
    }

    [PyMethod("throw")]
    [PyFunctionParameters("value", "/")]
    private static PyResult Throw(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0]);
    }

    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }

    // CPython coro_get_name/coro_set_name: created from the code object,
    // writable to str only, deletion rejected
    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyGeneratorObject self)
    {
        return self._pyNameOverride ?? PyStrObject.FromString(self.Name);
    }

    [PyProperty(PySpecialNames.Name, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Name(PyCallContext context, PyGeneratorObject self, PyObject value)
    {
        if (value is not PyStrObject str)
            return PyResult.TypeError("__name__ must be set to a string object");

        self._pyNameOverride = str;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Name, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Name(PyCallContext context, PyGeneratorObject self)
    {
        return PyResult.TypeError("__name__ must be set to a string object");
    }

    [PyProperty(PySpecialNames.QualName)]
    private static PyResult Get_QualName(PyCallContext context, PyGeneratorObject self)
    {
        return self._pyQualNameOverride ?? PyStrObject.FromString(self.QualName);
    }

    [PyProperty(PySpecialNames.QualName, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_QualName(PyCallContext context, PyGeneratorObject self, PyObject value)
    {
        if (value is not PyStrObject str)
            return PyResult.TypeError("__qualname__ must be set to a string object");

        self._pyQualNameOverride = str;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.QualName, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_QualName(PyCallContext context, PyGeneratorObject self)
    {
        return PyResult.TypeError("__qualname__ must be set to a string object");
    }
}
