using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Text;

namespace PySharp.Modules.Builtins;

public abstract class PyExceptionType : PyTypeObject<PyExceptionObject>
{
    internal PyExceptionObject Create(PyObject? pyObject = null)
    {
        return PyExceptionObject.UnsafeCreate(this, pyObject is null ? [] : [pyObject]);
    }
}

public interface IPyException<TSelf> where TSelf : PyExceptionType, IPyException<TSelf>
{
    static abstract TSelf Shared { get; }
}

#region Base Classes

[PyException("BaseException", Bases = [typeof(PyObjectType)])]
public sealed partial class PyBaseExceptionObjectType : PyExceptionType
{
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython BaseException_new accepts and ignores keyword arguments;
        // the rejection lives in __init__, so a subclass with a custom
        // __init__ still receives the original instantiation kwargs.
        return new PyExceptionObject(cls, [.. args]);
    }

    // CPython BaseException_init rejects keywords and re-binds the args
    // tuple: when __new__ is overridden but __init__ is not, type_call
    // still calls the inherited __init__ with the original instantiation
    // arguments, so a custom __new__ does not lose e.args. This own slot
    // also keeps exception instances from falling under object's
    // excess-argument check on direct object.__init__ calls.
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Exception_TakesNoKeywordArguments, self.PyType.Name);

        self.Args = [.. args];
        return PyNoneObject.None;
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

    [PyProperty(PySpecialNames.Cause, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Cause(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        if (value is PyNoneObject)
        {
            self.Cause = null;
            return PyNoneObject.None;
        }

        if (!PyBaseExceptionObjectType.Shared.IsInstance(value))
            return PyResult.TypeError(PySR.Runtime_BaseException_CauseMustDerive);

        // CPython BaseException_cause_set routes through PyException_SetCause,
        // which marks the implicit context suppressed; assigning None does not
        self.Cause = (PyExceptionObject)value;
        self.SuppressContext = true;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Cause, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Cause(PyCallContext context, PyExceptionObject self)
    {
        return PyResult.TypeError(PySR.Runtime_BaseException_CauseMayNotBeDeleted);
    }

    [PyProperty(PySpecialNames.Context)]
    private static PyResult Get_Context(PyCallContext context, PyExceptionObject self)
    {
        return (PyObject?)self.Context ?? PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Context, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Context(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        if (value is PyNoneObject)
        {
            self.Context = null;
            return PyNoneObject.None;
        }

        if (!PyBaseExceptionObjectType.Shared.IsInstance(value))
            return PyResult.TypeError(PySR.Runtime_BaseException_ContextMustDerive);

        self.Context = (PyExceptionObject)value;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Context, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Context(PyCallContext context, PyExceptionObject self)
    {
        return PyResult.TypeError(PySR.Runtime_BaseException_ContextMayNotBeDeleted);
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

    [PyProperty(PySpecialNames.SuppressContext, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_SuppressContext(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        if (value is not PyBoolObject flag)
            return PyResult.TypeError(PySR.Runtime_BaseException_SuppressMustBeBool);

        self.SuppressContext = flag.BoolValue;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.SuppressContext, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_SuppressContext(PyCallContext context, PyExceptionObject self)
    {
        return PyResult.TypeError(PySR.Runtime_BaseException_SuppressMayNotBeDeleted);
    }

    [PyProperty("args")]
    private static PyResult Get_Args(PyCallContext context, PyExceptionObject self)
    {
        return PyTupleObject.CreateTuple(self.Args);
    }
}

[PyException("Exception", Bases = [typeof(PyBaseExceptionObjectType)])]
public sealed partial class PyExceptionObjectType : PyExceptionType;

[PyException("LookupError")]
public sealed partial class PyLookupErrorObjectType : PyExceptionType;

[PyException("ArithmeticError")]
public sealed partial class PyArithmeticErrorObjectType : PyExceptionType;

#endregion Base Classes

#region Concrete Exceptions

[PyException("SystemExit", Bases = [typeof(PyBaseExceptionObjectType)])]
public sealed partial class PySystemExitObjectType : PyExceptionType
{
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateEmptyKwargs(kwargs, out var err))
            return err.Value;

        // CPython SystemExit_init: code is args[0] for a single argument,
        // the whole args tuple for multiple arguments, None for none;
        // assignment and deletion never fall back to args
        self.ExtraValue = args.Count switch
        {
            0 => PyNoneObject.None,
            1 => args[0],
            _ => PyTupleObject.CreateTuple(args),
        };
        return PyNoneObject.None;
    }

    [PyProperty("code")]
    private static PyResult Get_Code(PyCallContext context, PyExceptionObject self)
    {
        return self.ExtraValue ?? PyNoneObject.None;
    }

    [PyProperty("code", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Code(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        self.ExtraValue = value;
        return PyNoneObject.None;
    }

    [PyProperty("code", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Code(PyCallContext context, PyExceptionObject self)
    {
        self.ExtraValue = null;
        return PyNoneObject.None;
    }
}

[PyException("GeneratorExit", Bases = [typeof(PyBaseExceptionObjectType)])]
public sealed partial class PyGeneratorExitObjectType : PyExceptionType;

[PyException("TypeError")]
public sealed partial class PyTypeErrorObjectType : PyExceptionType;

[PyException("StopIteration")]
public sealed partial class PyStopIterationObjectType : PyExceptionType
{
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateEmptyKwargs(kwargs, out var err))
            return err.Value;

        self.ExtraValue = args.Count > 0 ? args[0] : PyNoneObject.None;
        return PyNoneObject.None;
    }

    [PyProperty("value")]
    private static PyResult Get_Value(PyCallContext context, PyExceptionObject self)
    {
        return self.ExtraValue ?? PyNoneObject.None;
    }

    [PyProperty("value", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Value(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        self.ExtraValue = value;
        return PyNoneObject.None;
    }

    [PyProperty("value", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Value(PyCallContext context, PyExceptionObject self)
    {
        self.ExtraValue = null;
        return PyNoneObject.None;
    }
}

[PyException("AttributeError")]
public sealed partial class PyAttributeErrorObjectType : PyExceptionType;

[PyException("KeyError", Bases = [typeof(PyLookupErrorObjectType)])]
public sealed partial class PyKeyErrorObjectType : PyExceptionType
{
    // CPython's KeyError_str applies repr() to a lone argument so quotes and
    // type information survive; every other arity keeps BaseException's form.
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        if (self.Args.Count is 1)
            return PySpecialMethods.Repr(context, self.Args[0]);

        if (self.Args.Count is 0)
            return PyStrObject.Empty;

        return PySpecialMethods.Str(context, PyTupleObject.CreateTuple(self.Args));
    }
}

[PyException("IndexError", Bases = [typeof(PyLookupErrorObjectType)])]
public sealed partial class PyIndexErrorObjectType : PyExceptionType;

[PyException("ValueError")]
public sealed partial class PyValueErrorObjectType : PyExceptionType;

[PyException("UnicodeError", Bases = [typeof(PyValueErrorObjectType)])]
public sealed partial class PyUnicodeErrorObjectType : PyExceptionType;

[PyException("UnicodeEncodeError", Bases = [typeof(PyUnicodeErrorObjectType)])]
public sealed partial class PyUnicodeEncodeErrorObjectType : PyExceptionType;

[PyException("NameError")]
public sealed partial class PyNameErrorObjectType : PyExceptionType;

[PyException("UnboundLocalError", Bases = [typeof(PyNameErrorObjectType)])]
public sealed partial class PyUnboundLocalErrorObjectType : PyExceptionType;

[PyException("ImportError")]
public sealed partial class PyImportErrorObjectType : PyExceptionType;

[PyException("ModuleNotFoundError", Bases = [typeof(PyImportErrorObjectType)])]
public sealed partial class PyModuleNotFoundErrorObjectType : PyExceptionType;

[PyException("SyntaxError")]
public sealed partial class PySyntaxErrorObjectType : PyExceptionType;

[PyException("IndentationError", Bases = [typeof(PySyntaxErrorObjectType)])]
public sealed partial class PyIndentationErrorObjectType : PyExceptionType;

[PyException("ZeroDivisionError", Bases = [typeof(PyArithmeticErrorObjectType)])]
public sealed partial class PyZeroDivisionErrorObjectType : PyExceptionType;

[PyException("AssertionError")]
public sealed partial class PyAssertionErrorObjectType : PyExceptionType;

[PyException("RuntimeError")]
public sealed partial class PyRuntimeErrorObjectType : PyExceptionType;

[PyException("KeyboardInterrupt", Bases = [typeof(PyBaseExceptionObjectType)])]
public sealed partial class PyKeyboardInterruptObjectType : PyExceptionType;

[PyException("FloatingPointError", Bases = [typeof(PyArithmeticErrorObjectType)])]
public sealed partial class PyFloatingPointErrorObjectType : PyExceptionType;

[PyException("OverflowError", Bases = [typeof(PyArithmeticErrorObjectType)])]
public sealed partial class PyOverflowErrorObjectType : PyExceptionType;

[PyException("BufferError")]
public sealed partial class PyBufferErrorObjectType : PyExceptionType;

[PyException("EOFError")]
public sealed partial class PyEOFErrorObjectType : PyExceptionType;

[PyException("MemoryError")]
public sealed partial class PyMemoryErrorObjectType : PyExceptionType;

[PyException("OSError")]
public sealed partial class PyOSErrorObjectType : PyExceptionType;

[PyException("BlockingIOError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyBlockingIOErrorObjectType : PyExceptionType;

[PyException("ChildProcessError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyChildProcessErrorObjectType : PyExceptionType;

[PyException("ConnectionError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyConnectionErrorObjectType : PyExceptionType;

[PyException("BrokenPipeError", Bases = [typeof(PyConnectionErrorObjectType)])]
public sealed partial class PyBrokenPipeErrorObjectType : PyExceptionType;

[PyException("ConnectionAbortedError", Bases = [typeof(PyConnectionErrorObjectType)])]
public sealed partial class PyConnectionAbortedErrorObjectType : PyExceptionType;

[PyException("ConnectionRefusedError", Bases = [typeof(PyConnectionErrorObjectType)])]
public sealed partial class PyConnectionRefusedErrorObjectType : PyExceptionType;

[PyException("ConnectionResetError", Bases = [typeof(PyConnectionErrorObjectType)])]
public sealed partial class PyConnectionResetErrorObjectType : PyExceptionType;

[PyException("FileExistsError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyFileExistsErrorObjectType : PyExceptionType;

[PyException("FileNotFoundError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyFileNotFoundErrorObjectType : PyExceptionType;

[PyException("InterruptedError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyInterruptedErrorObjectType : PyExceptionType;

[PyException("IsADirectoryError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyIsADirectoryErrorObjectType : PyExceptionType;

[PyException("NotADirectoryError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyNotADirectoryErrorObjectType : PyExceptionType;

[PyException("PermissionError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyPermissionErrorObjectType : PyExceptionType;

[PyException("ProcessLookupError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyProcessLookupErrorObjectType : PyExceptionType;

[PyException("TimeoutError", Bases = [typeof(PyOSErrorObjectType)])]
public sealed partial class PyTimeoutErrorObjectType : PyExceptionType;

[PyException("ReferenceError")]
public sealed partial class PyReferenceErrorObjectType : PyExceptionType;

[PyException("NotImplementedError", Bases = [typeof(PyRuntimeErrorObjectType)])]
public sealed partial class PyNotImplementedErrorObjectType : PyExceptionType;

[PyException("PythonFinalizationError", Bases = [typeof(PyRuntimeErrorObjectType)])]
public sealed partial class PyPythonFinalizationErrorObjectType : PyExceptionType;

[PyException("RecursionError", Bases = [typeof(PyRuntimeErrorObjectType)])]
public sealed partial class PyRecursionErrorObjectType : PyExceptionType;

[PyException("StopAsyncIteration")]
public sealed partial class PyStopAsyncIterationObjectType : PyExceptionType;

[PyException("TabError", Bases = [typeof(PyIndentationErrorObjectType)])]
public sealed partial class PyTabErrorObjectType : PyExceptionType;

[PyException("SystemError")]
public sealed partial class PySystemErrorObjectType : PyExceptionType;

[PyException("UnicodeDecodeError", Bases = [typeof(PyUnicodeErrorObjectType)])]
public sealed partial class PyUnicodeDecodeErrorObjectType : PyExceptionType
{
    // CPython UnicodeDecodeError_init: (encoding, object, start, end, reason)
    // is validated and stored so the attributes and str() stay in sync
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Exception_TakesNoKeywordArguments, self.PyType.Name);

        if (args.Count is not 5)
            return PyResult.TypeError($"UnicodeDecodeError() takes exactly 5 arguments ({args.Count} given)");

        if (args[0] is not PyStrObject)
            return PyResult.TypeError($"UnicodeDecodeError() argument 1 must be str, not {args[0].PyType.Name}");
        if (args[1] is not PyBytesObject and not PyByteArrayObject)
            return PyResult.TypeError($"UnicodeDecodeError() argument 2 must be a bytes-like object, not {args[1].PyType.Name}");
        if (args[2] is not PyIntObject || args[3] is not PyIntObject)
            return PyResult.TypeError("an integer is required");
        if (args[4] is not PyStrObject)
            return PyResult.TypeError($"UnicodeDecodeError() argument 5 must be str, not {args[4].PyType.Name}");

        self.Args = [.. args];
        self.PyAttributes["encoding"] = args[0];
        self.PyAttributes["object"] = args[1];
        self.PyAttributes["start"] = args[2];
        self.PyAttributes["end"] = args[3];
        self.PyAttributes["reason"] = args[4];
        return PyNoneObject.None;
    }

    // CPython UnicodeDecodeError_str: a single bad byte shows byte value and
    // position, anything else shows a position range
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        if (!self.PyAttributes.TryGetValue("object", out var objectAttr) ||
            !self.PyAttributes.TryGetValue("encoding", out var encodingAttr) ||
            !self.PyAttributes.TryGetValue("start", out var startAttr) ||
            !self.PyAttributes.TryGetValue("end", out var endAttr) ||
            !self.PyAttributes.TryGetValue("reason", out var reasonAttr))
            return PyStrObject.Empty;

        var start = (PyIntObject)startAttr;
        var end = (PyIntObject)endAttr;
        ReadOnlySpan<byte> data = objectAttr switch
        {
            PyBytesObject bytes => bytes.AsSpan(),
            PyByteArrayObject byteArray => byteArray.AsSpan(),
            _ => default,
        };
        long len = data.Length;
        long i = (long)start.Value;
        long j = (long)end.Value;
        var encoding = ((PyStrObject)encodingAttr).Value;
        var reason = ((PyStrObject)reasonAttr).Value;

        if (i >= 0 && i < len && j >= 0 && j <= len && j == i + 1)
            return PyStrObject.FromString($"'{encoding}' codec can't decode byte 0x{data[(int)i]:x2} in position {i}: {reason}");
        return PyStrObject.FromString($"'{encoding}' codec can't decode bytes in position {i}-{j - 1}: {reason}");
    }
}

[PyException("UnicodeTranslateError", Bases = [typeof(PyUnicodeErrorObjectType)])]
public sealed partial class PyUnicodeTranslateErrorObjectType : PyExceptionType;

#endregion Concrete Exceptions

#region Warnings

[PyException("Warning")]
public sealed partial class PyWarningObjectType : PyExceptionType;

[PyException("UserWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyUserWarningObjectType : PyExceptionType;

[PyException("SyntaxWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PySyntaxWarningObjectType : PyExceptionType;

[PyException("DeprecationWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyDeprecationWarningObjectType : PyExceptionType;

[PyException("BytesWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyBytesWarningObjectType : PyExceptionType;

[PyException("EncodingWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyEncodingWarningObjectType : PyExceptionType;

[PyException("FutureWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyFutureWarningObjectType : PyExceptionType;

[PyException("ImportWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyImportWarningObjectType : PyExceptionType;

[PyException("PendingDeprecationWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyPendingDeprecationWarningObjectType : PyExceptionType;

[PyException("ResourceWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyResourceWarningObjectType : PyExceptionType;

[PyException("RuntimeWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyRuntimeWarningObjectType : PyExceptionType;

[PyException("UnicodeWarning", Bases = [typeof(PyWarningObjectType)])]
public sealed partial class PyUnicodeWarningObjectType : PyExceptionType;

#endregion
