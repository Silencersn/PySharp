using PySharp.Modules.Builtins;
using PySharp.Modules.IO;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Sys;

// module position "_io" like CPython's io stack types (the qual name stays
// bare; ReprName composes it as "_io.StdIo" from the module)
[PyType("StdIo", Module = "_io")]
public sealed partial class PyStdIoObjectType : PyTypeObject<PyStdIoObject>
{
    protected override PyResult Repr(PyCallContext context, PyStdIoObject self)
    {
        return PyStrObject.FromString($"<_io.StdIo name='{self._name}'>");
    }

    [PyMethod("read")]
    [PyFunctionParameters("size=-1", "/")]
    private static PyResult Read(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        var sizeObj = arguments[0];
        if (sizeObj is PyNoneObject)
            return self.Read(context);
        var size = PyFileObjectType.IndexOrError(context, sizeObj, out var sizeError);
        if (size is null)
        {
            if (sizeObj.PyType.Slots.Index is null)
                return PyResult.TypeError(PySR.Runtime_File_SizeIntegerOrNone, sizeObj.PyType.TpName);
            return sizeError;
        }
        var bigSize = size.Value;
        if (bigSize > long.MaxValue || bigSize < long.MinValue)
            return PyResult.OverflowError(PySR.Runtime_Index_CannotFitInt);
        if (bigSize > int.MaxValue || bigSize < int.MinValue)
            return self.Read(context); // beyond C int the limit never binds
        return self.Read(context, size.Int32Value);
    }

    [PyMethod("write")]
    [PyFunctionParameters("data", "/")]
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

    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        return self.Close();
    }

    [PyMethod("readline")]
    [PyFunctionParameters("size=-1", "/")]
    private static PyResult ReadLine(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        // unlike read(), readline's clinic int converter rejects None
        var size = PyFileObjectType.IndexOrError(context, arguments[0], out var sizeError);
        if (size is null)
            return sizeError;
        var bigSize = size.Value;
        if (bigSize > long.MaxValue || bigSize < long.MinValue)
            return PyResult.OverflowError(PySR.Runtime_Number_Int_TooLargeForSsize);
        if (bigSize > int.MaxValue || bigSize < int.MinValue)
            return self.ReadLine(); // beyond C int the limit never binds
        return self.ReadLine(size.Int32Value);
    }

    [PyMethod("readable")]
    [PyFunctionParameters()]
    private static PyResult Readable(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        var flag = self.IsReadable;
        if (self.IsClosed && flag)
            return PyResult.ValueError(PySR.Runtime_File_ClosedNoPeriod);
        return PyBoolObject.FromBoolean(flag);
    }

    [PyMethod("writable")]
    [PyFunctionParameters()]
    private static PyResult Writable(PyCallContext context, PyStdIoObject self, PyArguments arguments)
    {
        var flag = self.IsWritable;
        if (self.IsClosed && flag)
            return PyResult.ValueError(PySR.Runtime_File_ClosedNoPeriod);
        return PyBoolObject.FromBoolean(flag);
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
