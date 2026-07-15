using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Text;

namespace PySharp.Modules.Builtins;

public abstract class PyExceptionType : PyTypeObject<PyExceptionObject>
{
    public PyExceptionObject Create()
    {
        return new PyExceptionObject(this, []);
    }

    public PyExceptionObject Create(PyObject? pyObject)
    {
        return new PyExceptionObject(this, pyObject is null ? [] : [pyObject]);
    }

    public PyExceptionObject Create(params IEnumerable<PyObject> pyObjects)
    {
        return new PyExceptionObject(this, [.. pyObjects]);
    }
}

public interface IPyException<TSelf> where TSelf : PyExceptionType, IPyException<TSelf>
{
    static abstract TSelf Shared { get; }
}

#region Base Classes

[PyException("BaseException", Bases=[typeof(PyObjectType)])]
public sealed partial class PyBaseExceptionObjectType : PyExceptionType
{

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(null);

        return new PyExceptionObject(cls, [.. args]);
    }

    protected override PyResult Repr(PyCallContext context, PyExceptionObject self)
    {
        var builder = new StringBuilder();
        builder.Append(self.PyType.FullName);
        builder.Append('(');

        for (int i = 0; i < self.Args.Count; i++)
        {
            var result = PySpecialMethods.Repr(context, self.Args[i]);
            if (result.IsError)
                return result;

            if (i > 0)
                builder.Append(", ");
            builder.Append(result.Value.Value);
        }

        builder.Append(')');

        return PyStrObject.FromString(builder.ToString());
    }

    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        if (self.Args.Count is 0)
            return PyStrObject.Empty;

        if (self.Args.Count is 1)
            return PySpecialMethods.Str(context, self.Args[0]);

        return PySpecialMethods.Str(context, PyTupleObject.CreateTuple(self.Args));
    }

    [PyProperty(PySpecialNames.Cause)]
    private static PyResult Get_Cause(PyCallContext context, PyExceptionObject self)
    {
        return (PyObject?)self.Cause ?? PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Context)]
    private static PyResult Get_Context(PyCallContext context, PyExceptionObject self)
    {
        return (PyObject?)self.Context ?? PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Traceback)]
    private static PyResult Get_Traceback(PyCallContext context, PyExceptionObject self)
    {
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.SuppressContext)]
    private static PyResult Get_SuppressContext(PyCallContext context, PyExceptionObject self)
    {
        return PyBoolObject.FromBoolean(self.SuppressContext);
    }

    [PyProperty("args")]
    private static PyResult Get_Args(PyCallContext context, PyExceptionObject self)
    {
        return PyTupleObject.CreateTuple(self.Args);
    }
}

[PyException("Exception", Bases=[typeof(PyBaseExceptionObjectType)])]
public sealed partial class PyExceptionObjectType : PyExceptionType;

[PyException("LookupError")]
public sealed partial class PyLookupErrorObjectType : PyExceptionType;

[PyException("ArithmeticError")]
public sealed partial class PyArithmeticErrorObjectType : PyExceptionType;

#endregion Base Classes

#region Concrete Exceptions

[PyException("SystemExit", Bases=[typeof(PyBaseExceptionObjectType)])]
public sealed partial class PySystemExitObjectType : PyExceptionType;

[PyException("GeneratorExit", Bases=[typeof(PyBaseExceptionObjectType)])]
public sealed partial class PyGeneratorExitObjectType : PyExceptionType;

[PyException("TypeError")]
public sealed partial class PyTypeErrorObjectType : PyExceptionType;

[PyException("StopIteration")]
public sealed partial class PyStopIterationObjectType : PyExceptionType;

[PyException("AttributeError")]
public sealed partial class PyAttributeErrorObjectType : PyExceptionType;

[PyException("KeyError", Bases=[typeof(PyLookupErrorObjectType)])]
public sealed partial class PyKeyErrorObjectType : PyExceptionType;

[PyException("IndexError", Bases=[typeof(PyLookupErrorObjectType)])]
public sealed partial class PyIndexErrorObjectType : PyExceptionType;

[PyException("ValueError")]
public sealed partial class PyValueErrorObjectType : PyExceptionType;

[PyException("UnicodeError", Bases=[typeof(PyValueErrorObjectType)])]
public sealed partial class PyUnicodeErrorObjectType : PyExceptionType;

[PyException("UnicodeEncodeError", Bases=[typeof(PyUnicodeErrorObjectType)])]
public sealed partial class PyUnicodeEncodeErrorObjectType : PyExceptionType;

[PyException("NameError")]
public sealed partial class PyNameErrorObjectType : PyExceptionType;

[PyException("UnboundLocalError", Bases=[typeof(PyNameErrorObjectType)])]
public sealed partial class PyUnboundLocalErrorObjectType : PyExceptionType;

[PyException("ImportError")]
public sealed partial class PyImportErrorObjectType : PyExceptionType;

[PyException("ModuleNotFoundError", Bases=[typeof(PyImportErrorObjectType)])]
public sealed partial class PyModuleNotFoundErrorObjectType : PyExceptionType;

[PyException("SyntaxError")]
public sealed partial class PySyntaxErrorObjectType : PyExceptionType;

[PyException("IndentationError", Bases=[typeof(PySyntaxErrorObjectType)])]
public sealed partial class PyIndentationErrorObjectType : PyExceptionType;

[PyException("ZeroDivisionError", Bases=[typeof(PyArithmeticErrorObjectType)])]
public sealed partial class PyZeroDivisionErrorObjectType : PyExceptionType;

[PyException("AssertionError")]
public sealed partial class PyAssertionErrorObjectType : PyExceptionType;

[PyException("RuntimeError")]
public sealed partial class PyRuntimeErrorObjectType : PyExceptionType;

[PyException("KeyboardInterrupt", Bases=[typeof(PyBaseExceptionObjectType)])]
public sealed partial class PyKeyboardInterruptObjectType : PyExceptionType;

[PyException("FloatingPointError", Bases=[typeof(PyArithmeticErrorObjectType)])]
public sealed partial class PyFloatingPointErrorObjectType : PyExceptionType;

[PyException("OverflowError", Bases=[typeof(PyArithmeticErrorObjectType)])]
public sealed partial class PyOverflowErrorObjectType : PyExceptionType;

[PyException("BufferError")]
public sealed partial class PyBufferErrorObjectType : PyExceptionType;

[PyException("EOFError")]
public sealed partial class PyEOFErrorObjectType : PyExceptionType;

[PyException("MemoryError")]
public sealed partial class PyMemoryErrorObjectType : PyExceptionType;

[PyException("OSError")]
public sealed partial class PyOSErrorObjectType : PyExceptionType;

[PyException("BlockingIOError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyBlockingIOErrorObjectType : PyExceptionType;

[PyException("ChildProcessError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyChildProcessErrorObjectType : PyExceptionType;

[PyException("ConnectionError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyConnectionErrorObjectType : PyExceptionType;

[PyException("BrokenPipeError", Bases=[typeof(PyConnectionErrorObjectType)])]
public sealed partial class PyBrokenPipeErrorObjectType : PyExceptionType;

[PyException("ConnectionAbortedError", Bases=[typeof(PyConnectionErrorObjectType)])]
public sealed partial class PyConnectionAbortedErrorObjectType : PyExceptionType;

[PyException("ConnectionRefusedError", Bases=[typeof(PyConnectionErrorObjectType)])]
public sealed partial class PyConnectionRefusedErrorObjectType : PyExceptionType;

[PyException("ConnectionResetError", Bases=[typeof(PyConnectionErrorObjectType)])]
public sealed partial class PyConnectionResetErrorObjectType : PyExceptionType;

[PyException("FileExistsError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyFileExistsErrorObjectType : PyExceptionType;

[PyException("FileNotFoundError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyFileNotFoundErrorObjectType : PyExceptionType;

[PyException("InterruptedError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyInterruptedErrorObjectType : PyExceptionType;

[PyException("IsADirectoryError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyIsADirectoryErrorObjectType : PyExceptionType;

[PyException("NotADirectoryError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyNotADirectoryErrorObjectType : PyExceptionType;

[PyException("PermissionError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyPermissionErrorObjectType : PyExceptionType;

[PyException("ProcessLookupError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyProcessLookupErrorObjectType : PyExceptionType;

[PyException("TimeoutError", Bases=[typeof(PyOSErrorObjectType)])]
public sealed partial class PyTimeoutErrorObjectType : PyExceptionType;

[PyException("ReferenceError")]
public sealed partial class PyReferenceErrorObjectType : PyExceptionType;

[PyException("NotImplementedError", Bases=[typeof(PyRuntimeErrorObjectType)])]
public sealed partial class PyNotImplementedErrorObjectType : PyExceptionType;

[PyException("PythonFinalizationError", Bases=[typeof(PyRuntimeErrorObjectType)])]
public sealed partial class PyPythonFinalizationErrorObjectType : PyExceptionType;

[PyException("RecursionError", Bases=[typeof(PyRuntimeErrorObjectType)])]
public sealed partial class PyRecursionErrorObjectType : PyExceptionType;

[PyException("StopAsyncIteration")]
public sealed partial class PyStopAsyncIterationObjectType : PyExceptionType;

[PyException("TabError", Bases=[typeof(PyIndentationErrorObjectType)])]
public sealed partial class PyTabErrorObjectType : PyExceptionType;

[PyException("SystemError")]
public sealed partial class PySystemErrorObjectType : PyExceptionType;

[PyException("UnicodeDecodeError", Bases=[typeof(PyUnicodeErrorObjectType)])]
public sealed partial class PyUnicodeDecodeErrorObjectType : PyExceptionType;

[PyException("UnicodeTranslateError", Bases=[typeof(PyUnicodeErrorObjectType)])]
public sealed partial class PyUnicodeTranslateErrorObjectType : PyExceptionType;

#endregion Concrete Exceptions

#region Warnings

[PyException("Warning")]
public sealed partial class PyWarningObjectType : PyExceptionType;

[PyException("UserWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyUserWarningObjectType : PyExceptionType;

[PyException("SyntaxWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PySyntaxWarningObjectType : PyExceptionType;

[PyException("DeprecationWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyDeprecationWarningObjectType : PyExceptionType;

[PyException("BytesWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyBytesWarningObjectType : PyExceptionType;

[PyException("EncodingWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyEncodingWarningObjectType : PyExceptionType;

[PyException("FutureWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyFutureWarningObjectType : PyExceptionType;

[PyException("ImportWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyImportWarningObjectType : PyExceptionType;

[PyException("PendingDeprecationWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyPendingDeprecationWarningObjectType : PyExceptionType;

[PyException("ResourceWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyResourceWarningObjectType : PyExceptionType;

[PyException("RuntimeWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyRuntimeWarningObjectType : PyExceptionType;

[PyException("UnicodeWarning", Bases=[typeof(PyWarningObjectType)])]
public sealed partial class PyUnicodeWarningObjectType : PyExceptionType;

#endregion
