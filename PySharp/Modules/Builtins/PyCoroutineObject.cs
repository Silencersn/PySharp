using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Modules.Builtins;

[PyType("coroutine")]
public sealed partial class PyCoroutineObjectType : PyTypeObject<PyCoroutineObjectType, PyGeneratorObject>
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
    [PyFunctionArgsDef("value")]
    private static PyResult Send(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        if (arguments[0] is PyNoneObject)
            return self.PyNext(context);

        return self.PySend(context, arguments[0]);
    }

    [PyMethod("throw")]
    [PyFunctionArgsDef("value")]
    private static PyResult Throw(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyThrow(context, arguments[0]);
    }

    [PyMethod("close")]
    [PyFunctionArgsDef()]
    private static PyResult Close(PyCallContext context, PyGeneratorObject self, PyArguments arguments)
    {
        return self.PyClose(context);
    }
}