using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Sys;

[PyType("_io.StdIo")]
public sealed partial class PyStdIoObjectType : PyTypeObject<PyStdIoObject>
{
    protected override PyResult Repr(PyCallContext context, PyStdIoObject self)
    {
        return PyStrObject.FromString($"<_io.StdIo name='{self._name}'>");
    }

    [PyMethod("read")]
    [PyFunctionParameters("size=-1")]
    private static PyResult Read(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        var sizeObj = arguments[0];
        if (sizeObj is PyIntObject intObj)
            return self.Read(context, intObj.Int32Value);
        return self.Read(context);
    }

    [PyMethod("write")]
    [PyFunctionParameters("data")]
    private static PyResult Write(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        return self.Write(context, arguments[0]);
    }

    [PyMethod("flush")]
    [PyFunctionParameters()]
    private static PyResult Flush(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        return self.Flush();
    }

    [PyMethod("readline")]
    [PyFunctionParameters("size=-1")]
    private static PyResult ReadLine(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        var sizeObj = arguments[0];
        if (sizeObj is PyIntObject intObj)
            return self.ReadLine(intObj.Int32Value);
        return self.ReadLine();
    }

    [PyMethod("readable")]
    [PyFunctionParameters()]
    private static PyResult Readable(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.IsReadable);
    }

    [PyMethod("writable")]
    [PyFunctionParameters()]
    private static PyResult Writable(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self.IsWritable);
    }

    [PyProperty("closed")]
    private static PyResult Get_closed(PyCallContext context, PyStdIoObject self)
    {
        return PyBoolObject.FromBoolean(self.IsClosed);
    }

    [PyProperty("name")]
    private static PyResult Get_name(PyCallContext context, PyStdIoObject self)
    {
        return PyStrObject.FromString(self._name);
    }
}
