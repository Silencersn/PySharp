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

public abstract class PyExceptionType<TSelf> : PyExceptionType, IPyException<TSelf> where TSelf : PyExceptionType<TSelf>, new()
{
    public static TSelf Shared { get; } = new TSelf();
}

public abstract class PyExceptionType<TSelf, TBase> : PyExceptionType<TSelf>
    where TSelf : PyExceptionType<TSelf, TBase>, new()
    where TBase : PyExceptionType<TBase>, IPyException<TBase>, new()
{
    public sealed override IReadOnlyList<PyTypeObject> Bases => [TBase.Shared];
}

#region Base Classes

[PyType("BaseException", CustomConstructor = true)]
public sealed partial class PyBaseExceptionObjectType : PyExceptionType<PyBaseExceptionObjectType>
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
}

[PyType("Exception", CustomConstructor = true)]
public sealed partial class PyExceptionObjectType : PyExceptionType<PyExceptionObjectType, PyBaseExceptionObjectType>;

[PyType("LookupError", CustomConstructor = true)]
public sealed partial class PyLookupErrorObjectType : PyExceptionType<PyLookupErrorObjectType, PyExceptionObjectType>;

[PyType("ArithmeticError", CustomConstructor = true)]
public sealed partial class PyArithmeticErrorObjectType : PyExceptionType<PyArithmeticErrorObjectType, PyExceptionObjectType>;

#endregion Base Classes

#region Concrete Exceptions

[PyType("SystemExit", CustomConstructor = true)]
public sealed partial class PySystemExitObjectType : PyExceptionType<PySystemExitObjectType, PyBaseExceptionObjectType>;

[PyType("GeneratorExit", CustomConstructor = true)]
public sealed partial class PyGeneratorExitObjectType : PyExceptionType<PyGeneratorExitObjectType, PyBaseExceptionObjectType>;

[PyType("TypeError", CustomConstructor = true)]
public sealed partial class PyTypeErrorObjectType : PyExceptionType<PyTypeErrorObjectType, PyExceptionObjectType>;

[PyType("StopIteration", CustomConstructor = true)]
public sealed partial class PyStopIterationObjectType : PyExceptionType<PyStopIterationObjectType, PyExceptionObjectType>;

[PyType("AttributeError", CustomConstructor = true)]
public sealed partial class PyAttributeErrorObjectType : PyExceptionType<PyAttributeErrorObjectType, PyExceptionObjectType>;

[PyType("KeyError", CustomConstructor = true)]
public sealed partial class PyKeyErrorObjectType : PyExceptionType<PyKeyErrorObjectType, PyLookupErrorObjectType>;

[PyType("IndexError", CustomConstructor = true)]
public sealed partial class PyIndexErrorObjectType : PyExceptionType<PyIndexErrorObjectType, PyLookupErrorObjectType>;

[PyType("ValueError", CustomConstructor = true)]
public sealed partial class PyValueErrorObjectType : PyExceptionType<PyValueErrorObjectType, PyExceptionObjectType>;

[PyType("UnicodeError", CustomConstructor = true)]
public sealed partial class PyUnicodeErrorObjectType : PyExceptionType<PyUnicodeErrorObjectType, PyValueErrorObjectType>;

[PyType("UnicodeEncodeError", CustomConstructor = true)]
public sealed partial class PyUnicodeEncodeErrorObjectType : PyExceptionType<PyUnicodeEncodeErrorObjectType, PyUnicodeErrorObjectType>;

[PyType("NameError", CustomConstructor = true)]
public sealed partial class PyNameErrorObjectType : PyExceptionType<PyNameErrorObjectType, PyExceptionObjectType>;

[PyType("UnboundLocalError", CustomConstructor = true)]
public sealed partial class PyUnboundLocalErrorObjectType : PyExceptionType<PyUnboundLocalErrorObjectType, PyNameErrorObjectType>;

[PyType("ImportError", CustomConstructor = true)]
public sealed partial class PyImportErrorObjectType : PyExceptionType<PyImportErrorObjectType, PyExceptionObjectType>;

[PyType("ModuleNotFoundError", CustomConstructor = true)]
public sealed partial class PyModuleNotFoundErrorObjectType : PyExceptionType<PyModuleNotFoundErrorObjectType, PyImportErrorObjectType>;

[PyType("SyntaxError", CustomConstructor = true)]
public sealed partial class PySyntaxErrorObjectType : PyExceptionType<PySyntaxErrorObjectType, PyExceptionObjectType>;

[PyType("IndentationError", CustomConstructor = true)]
public sealed partial class PyIndentationErrorObjectType : PyExceptionType<PyIndentationErrorObjectType, PySyntaxErrorObjectType>;

[PyType("ZeroDivisionError", CustomConstructor = true)]
public sealed partial class PyZeroDivisionErrorObjectType : PyExceptionType<PyZeroDivisionErrorObjectType, PyArithmeticErrorObjectType>;

[PyType("AssertionError", CustomConstructor = true)]
public sealed partial class PyAssertionErrorObjectType : PyExceptionType<PyAssertionErrorObjectType, PyExceptionObjectType>;

[PyType("RuntimeError", CustomConstructor = true)]
public sealed partial class PyRuntimeErrorObjectType : PyExceptionType<PyRuntimeErrorObjectType, PyExceptionObjectType>;

[PyType("KeyboardInterrupt", CustomConstructor = true)]
public sealed partial class PyKeyboardInterruptObjectType : PyExceptionType<PyKeyboardInterruptObjectType, PyBaseExceptionObjectType>;

[PyType("FloatingPointError", CustomConstructor = true)]
public sealed partial class PyFloatingPointErrorObjectType : PyExceptionType<PyFloatingPointErrorObjectType, PyArithmeticErrorObjectType>;

[PyType("OverflowError", CustomConstructor = true)]
public sealed partial class PyOverflowErrorObjectType : PyExceptionType<PyOverflowErrorObjectType, PyArithmeticErrorObjectType>;

[PyType("BufferError", CustomConstructor = true)]
public sealed partial class PyBufferErrorObjectType : PyExceptionType<PyBufferErrorObjectType, PyExceptionObjectType>;

[PyType("EOFError", CustomConstructor = true)]
public sealed partial class PyEOFErrorObjectType : PyExceptionType<PyEOFErrorObjectType, PyExceptionObjectType>;

[PyType("MemoryError", CustomConstructor = true)]
public sealed partial class PyMemoryErrorObjectType : PyExceptionType<PyMemoryErrorObjectType, PyExceptionObjectType>;

[PyType("OSError", CustomConstructor = true)]
public sealed partial class PyOSErrorObjectType : PyExceptionType<PyOSErrorObjectType, PyExceptionObjectType>;

[PyType("BlockingIOError", CustomConstructor = true)]
public sealed partial class PyBlockingIOErrorObjectType : PyExceptionType<PyBlockingIOErrorObjectType, PyOSErrorObjectType>;

[PyType("ChildProcessError", CustomConstructor = true)]
public sealed partial class PyChildProcessErrorObjectType : PyExceptionType<PyChildProcessErrorObjectType, PyOSErrorObjectType>;

[PyType("ConnectionError", CustomConstructor = true)]
public sealed partial class PyConnectionErrorObjectType : PyExceptionType<PyConnectionErrorObjectType, PyOSErrorObjectType>;

[PyType("BrokenPipeError", CustomConstructor = true)]
public sealed partial class PyBrokenPipeErrorObjectType : PyExceptionType<PyBrokenPipeErrorObjectType, PyConnectionErrorObjectType>;

[PyType("ConnectionAbortedError", CustomConstructor = true)]
public sealed partial class PyConnectionAbortedErrorObjectType : PyExceptionType<PyConnectionAbortedErrorObjectType, PyConnectionErrorObjectType>;

[PyType("ConnectionRefusedError", CustomConstructor = true)]
public sealed partial class PyConnectionRefusedErrorObjectType : PyExceptionType<PyConnectionRefusedErrorObjectType, PyConnectionErrorObjectType>;

[PyType("ConnectionResetError", CustomConstructor = true)]
public sealed partial class PyConnectionResetErrorObjectType : PyExceptionType<PyConnectionResetErrorObjectType, PyConnectionErrorObjectType>;

[PyType("FileExistsError", CustomConstructor = true)]
public sealed partial class PyFileExistsErrorObjectType : PyExceptionType<PyFileExistsErrorObjectType, PyOSErrorObjectType>;

[PyType("FileNotFoundError", CustomConstructor = true)]
public sealed partial class PyFileNotFoundErrorObjectType : PyExceptionType<PyFileNotFoundErrorObjectType, PyOSErrorObjectType>;

[PyType("InterruptedError", CustomConstructor = true)]
public sealed partial class PyInterruptedErrorObjectType : PyExceptionType<PyInterruptedErrorObjectType, PyOSErrorObjectType>;

[PyType("IsADirectoryError", CustomConstructor = true)]
public sealed partial class PyIsADirectoryErrorObjectType : PyExceptionType<PyIsADirectoryErrorObjectType, PyOSErrorObjectType>;

[PyType("NotADirectoryError", CustomConstructor = true)]
public sealed partial class PyNotADirectoryErrorObjectType : PyExceptionType<PyNotADirectoryErrorObjectType, PyOSErrorObjectType>;

[PyType("PermissionError", CustomConstructor = true)]
public sealed partial class PyPermissionErrorObjectType : PyExceptionType<PyPermissionErrorObjectType, PyOSErrorObjectType>;

[PyType("ProcessLookupError", CustomConstructor = true)]
public sealed partial class PyProcessLookupErrorObjectType : PyExceptionType<PyProcessLookupErrorObjectType, PyOSErrorObjectType>;

[PyType("TimeoutError", CustomConstructor = true)]
public sealed partial class PyTimeoutErrorObjectType : PyExceptionType<PyTimeoutErrorObjectType, PyOSErrorObjectType>;

[PyType("ReferenceError", CustomConstructor = true)]
public sealed partial class PyReferenceErrorObjectType : PyExceptionType<PyReferenceErrorObjectType, PyExceptionObjectType>;

[PyType("NotImplementedError", CustomConstructor = true)]
public sealed partial class PyNotImplementedErrorObjectType : PyExceptionType<PyNotImplementedErrorObjectType, PyRuntimeErrorObjectType>;

[PyType("PythonFinalizationError", CustomConstructor = true)]
public sealed partial class PyPythonFinalizationErrorObjectType : PyExceptionType<PyPythonFinalizationErrorObjectType, PyRuntimeErrorObjectType>;

[PyType("RecursionError", CustomConstructor = true)]
public sealed partial class PyRecursionErrorObjectType : PyExceptionType<PyRecursionErrorObjectType, PyRuntimeErrorObjectType>;

[PyType("StopAsyncIteration", CustomConstructor = true)]
public sealed partial class PyStopAsyncIterationObjectType : PyExceptionType<PyStopAsyncIterationObjectType, PyExceptionObjectType>;

[PyType("TabError", CustomConstructor = true)]
public sealed partial class PyTabErrorObjectType : PyExceptionType<PyTabErrorObjectType, PyIndentationErrorObjectType>;

[PyType("SystemError", CustomConstructor = true)]
public sealed partial class PySystemErrorObjectType : PyExceptionType<PySystemErrorObjectType, PyExceptionObjectType>;

[PyType("UnicodeDecodeError", CustomConstructor = true)]
public sealed partial class PyUnicodeDecodeErrorObjectType : PyExceptionType<PyUnicodeDecodeErrorObjectType, PyUnicodeErrorObjectType>;

[PyType("UnicodeTranslateError", CustomConstructor = true)]
public sealed partial class PyUnicodeTranslateErrorObjectType : PyExceptionType<PyUnicodeTranslateErrorObjectType, PyUnicodeErrorObjectType>;

#endregion Concrete Exceptions

#region Warnings

[PyType("Warning", CustomConstructor = true)]
public sealed partial class PyWarningObjectType : PyExceptionType<PyWarningObjectType, PyExceptionObjectType>;

[PyType("UserWarning", CustomConstructor = true)]
public sealed partial class PyUserWarningObjectType : PyExceptionType<PyUserWarningObjectType, PyWarningObjectType>;

[PyType("SyntaxWarning", CustomConstructor = true)]
public sealed partial class PySyntaxWarningObjectType : PyExceptionType<PySyntaxWarningObjectType, PyWarningObjectType>;

[PyType("DeprecationWarning", CustomConstructor = true)]
public sealed partial class PyDeprecationWarningObjectType : PyExceptionType<PyDeprecationWarningObjectType, PyWarningObjectType>;

[PyType("BytesWarning", CustomConstructor = true)]
public sealed partial class PyBytesWarningObjectType : PyExceptionType<PyBytesWarningObjectType, PyWarningObjectType>;

[PyType("EncodingWarning", CustomConstructor = true)]
public sealed partial class PyEncodingWarningObjectType : PyExceptionType<PyEncodingWarningObjectType, PyWarningObjectType>;

[PyType("FutureWarning", CustomConstructor = true)]
public sealed partial class PyFutureWarningObjectType : PyExceptionType<PyFutureWarningObjectType, PyWarningObjectType>;

[PyType("ImportWarning", CustomConstructor = true)]
public sealed partial class PyImportWarningObjectType : PyExceptionType<PyImportWarningObjectType, PyWarningObjectType>;

[PyType("PendingDeprecationWarning", CustomConstructor = true)]
public sealed partial class PyPendingDeprecationWarningObjectType : PyExceptionType<PyPendingDeprecationWarningObjectType, PyWarningObjectType>;

[PyType("ResourceWarning", CustomConstructor = true)]
public sealed partial class PyResourceWarningObjectType : PyExceptionType<PyResourceWarningObjectType, PyWarningObjectType>;

[PyType("RuntimeWarning", CustomConstructor = true)]
public sealed partial class PyRuntimeWarningObjectType : PyExceptionType<PyRuntimeWarningObjectType, PyWarningObjectType>;

[PyType("UnicodeWarning", CustomConstructor = true)]
public sealed partial class PyUnicodeWarningObjectType : PyExceptionType<PyUnicodeWarningObjectType, PyWarningObjectType>;

#endregion
