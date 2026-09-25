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
        // BaseException_repr names the type with _PyType_Name
        // (Objects/exceptions.c:200), i.e. the bare name — a class defined in a
        // function reprs as "LocalError('m')", never "f.<locals>.LocalError"
        builder.Append(self.PyType.Name);
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
        return BaseExceptionStr(context, self);
    }

    // BaseException_str: empty for no arguments, the lone argument's str
    // for one, the args tuple form beyond that. Shared with the subclass
    // __str__ fallthroughs, which cannot reach this override through the
    // PyExceptionType base class.
    internal static PyResult BaseExceptionStr(PyCallContext context, PyExceptionObject self)
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
        // CPython keeps self->args by reference, so a tuple assigned
        // through the setter is handed back as the same object
        if (self.Args is PyTupleObject tuple)
            return tuple;
        return PyTupleObject.CreateTuple(self.Args);
    }

    // CPython BaseException_set_args: the value goes through
    // PySequence_Tuple, so a tuple is kept by identity and any other
    // iterable is drained into a fresh tuple; a non-iterable fails with
    // the plain iteration error. str()/repr() read args live, so the
    // new value shows up immediately.
    [PyProperty("args", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Args(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        if (value is PyTupleObject tuple)
        {
            // Storing the tuple itself keeps the getter identity fast
            // path working: the assigned tuple comes back as the same
            // object, like CPython keeping self->args by reference
            self.Args = tuple;
            return PyNoneObject.None;
        }

        var listResult = PyUtils.IteratorToListWithHint(context, value);
        if (listResult.IsError)
            return listResult;

        self.Args = listResult.Value;
        return PyNoneObject.None;
    }

    // CPython BaseException.args defines no deleter
    [PyProperty("args", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Args(PyCallContext context, PyExceptionObject self)
    {
        return PyResult.TypeError("args may not be deleted");
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
public sealed partial class PyUnicodeEncodeErrorObjectType : PyExceptionType
{
    // CPython UnicodeEncodeError_init: BaseException_init keeps the full
    // args tuple, then the "UUnnU" parse validates encoding/object/reason
    // as str and start/end as index integers.
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Exception_TakesNoKeywordArguments, self.PyType.Name);

        self.Args = [.. args];

        if (args.Count is not 5)
            return PyResult.TypeError($"function takes exactly 5 arguments ({args.Count} given)");
        if (args[0] is not PyStrObject)
            return PyResult.TypeError($"argument 1 must be str, not {args[0].PyType.Name}");
        if (args[1] is not PyStrObject)
            return PyResult.TypeError($"argument 2 must be str, not {args[1].PyType.Name}");
        if (args[2] is not PyIntObject)
            return PyResult.TypeError($"'{args[2].PyType.Name}' object cannot be interpreted as an integer");
        if (args[3] is not PyIntObject)
            return PyResult.TypeError($"'{args[3].PyType.Name}' object cannot be interpreted as an integer");
        if (args[4] is not PyStrObject)
            return PyResult.TypeError($"argument 5 must be str, not {args[4].PyType.Name}");

        self.PyAttributes["encoding"] = args[0];
        self.PyAttributes["object"] = args[1];
        self.PyAttributes["start"] = args[2];
        self.PyAttributes["end"] = args[3];
        self.PyAttributes["reason"] = args[4];
        return PyNoneObject.None;
    }

    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        return PyUnicodeErrorStr.Format(context, self, withEncoding: true);
    }
}

[PyException("NameError")]
public sealed partial class PyNameErrorObjectType : PyExceptionType;

[PyException("UnboundLocalError", Bases = [typeof(PyNameErrorObjectType)])]
public sealed partial class PyUnboundLocalErrorObjectType : PyExceptionType;

[PyException("ImportError")]
public sealed partial class PyImportErrorObjectType : PyExceptionType
{
    // CPython ImportError_init (Objects/exceptions.c:1810): BaseException_init
    // runs first, then the name/path/name_from keywords are parsed off an
    // empty positional tuple — so positional arguments set args as usual and
    // only these three keywords are accepted. msg is args[0] when there is
    // exactly one argument, otherwise it stays unset (ImportError_str falls
    // back to BaseException's args rendering in that case).
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        self.Args = [.. args];

        foreach (var (key, value) in kwargs)
        {
            if (key is not ("name" or "path" or "name_from"))
                // CPython parses the keywords with the literal format
                // "|$OOO:ImportError", so a subclass is still reported under
                // the base name
                return PyResult.TypeError(PySR.Runtime_Import_ErrorUnexpectedKeyword, key);

            self.PyAttributes[key] = value;
        }

        if (args.Count is 1)
            self.PyAttributes["msg"] = args[0];

        return PyNoneObject.None;
    }

    // CPython ImportError_str: an exact str msg wins over the args rendering,
    // so a one-argument str() is the message even when args carries more
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        if (self.PyAttributes.TryGetValue("msg", out var msg) && msg.PyType is PyStrObjectType)
            return PySpecialMethods.Str(context, msg);

        return PyBaseExceptionObjectType.BaseExceptionStr(context, self);
    }

    // The rest are plain members (Objects/exceptions.c ImportError_members):
    // assignment and deletion go through __dict__ and are never rejected, and
    // an unset member reads back as None.
    [PyProperty("msg")]
    private static PyResult Get_Msg(PyCallContext context, PyExceptionObject self)
    {
        return self.PyAttributes.TryGetValue("msg", out var msg) ? msg : PyNoneObject.None;
    }

    [PyProperty("msg", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Msg(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        self.PyAttributes["msg"] = value;
        return PyNoneObject.None;
    }

    [PyProperty("msg", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Msg(PyCallContext context, PyExceptionObject self)
    {
        self.PyAttributes.Remove("msg");
        return PyNoneObject.None;
    }

    [PyProperty("name")]
    private static PyResult Get_Name(PyCallContext context, PyExceptionObject self)
    {
        return self.PyAttributes.TryGetValue("name", out var name) ? name : PyNoneObject.None;
    }

    [PyProperty("name", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Name(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        self.PyAttributes["name"] = value;
        return PyNoneObject.None;
    }

    [PyProperty("name", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Name(PyCallContext context, PyExceptionObject self)
    {
        self.PyAttributes.Remove("name");
        return PyNoneObject.None;
    }

    [PyProperty("path")]
    private static PyResult Get_Path(PyCallContext context, PyExceptionObject self)
    {
        return self.PyAttributes.TryGetValue("path", out var path) ? path : PyNoneObject.None;
    }

    [PyProperty("path", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Path(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        self.PyAttributes["path"] = value;
        return PyNoneObject.None;
    }

    [PyProperty("path", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Path(PyCallContext context, PyExceptionObject self)
    {
        self.PyAttributes.Remove("path");
        return PyNoneObject.None;
    }

    [PyProperty("name_from")]
    private static PyResult Get_NameFrom(PyCallContext context, PyExceptionObject self)
    {
        return self.PyAttributes.TryGetValue("name_from", out var nameFrom) ? nameFrom : PyNoneObject.None;
    }

    [PyProperty("name_from", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_NameFrom(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        self.PyAttributes["name_from"] = value;
        return PyNoneObject.None;
    }

    [PyProperty("name_from", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_NameFrom(PyCallContext context, PyExceptionObject self)
    {
        self.PyAttributes.Remove("name_from");
        return PyNoneObject.None;
    }
}

[PyException("ModuleNotFoundError", Bases = [typeof(PyImportErrorObjectType)])]
public sealed partial class PyModuleNotFoundErrorObjectType : PyExceptionType;

[PyException("SyntaxError")]
public sealed partial class PySyntaxErrorObjectType : PyExceptionType
{
    // CPython SyntaxError_init: BaseException_init runs first, so args
    // keep the full tuple; args[0] becomes msg and a two-argument call
    // parses args[1] as the (filename, lineno, offset, text[, end_lineno,
    // end_offset, _metadata]) info tuple.
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Exception_TakesNoKeywordArguments, self.PyType.Name);

        self.Args = [.. args];

        if (args.Count >= 1)
            self.PyAttributes["msg"] = args[0];

        if (args.Count is not 2)
            return PyNoneObject.None;

        var infoResult = PyUtils.IteratorToListWithHint(context, args[1]);
        if (infoResult.IsError)
            return infoResult;
        var info = infoResult.Value;

        if (info.Count < 4)
            return PyResult.TypeError($"function takes at least 4 arguments ({info.Count} given)");
        if (info.Count > 7)
            return PyResult.TypeError($"function takes at most 7 arguments ({info.Count} given)");

        self.PyAttributes["filename"] = info[0];
        self.PyAttributes["lineno"] = info[1];
        self.PyAttributes["offset"] = info[2];
        self.PyAttributes["text"] = info[3];

        if (info.Count is 5)
            return PyResult.TypeError("end_offset must be provided when end_lineno is provided");
        if (info.Count >= 6)
        {
            self.PyAttributes["end_lineno"] = info[4];
            self.PyAttributes["end_offset"] = info[5];
        }
        if (info.Count is 7)
            self.PyAttributes["_metadata"] = info[6];

        return PyNoneObject.None;
    }

    // CPython SyntaxError_str: the filename only renders when it is a str,
    // reduced to its basename on the platform separator; the line number
    // only counts when it is an exact int. Without either, the message
    // degenerates to str(msg) — None included.
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        var attrs = self.PyAttributes;
        PyObject msg = attrs.TryGetValue("msg", out var msgValue) ? msgValue : PyNoneObject.None;

        string? basename = null;
        if (attrs.TryGetValue("filename", out var filenameValue) && filenameValue is PyStrObject filenameStr)
            basename = Basename(filenameStr.Value);

        bool haveLine = attrs.TryGetValue("lineno", out var linenoValue) &&
                        linenoValue is PyIntObject &&
                        linenoValue is not PyBoolObject;

        if (basename is null && !haveLine)
            return PySpecialMethods.Str(context, msg);

        var msgStr = PySpecialMethods.Str(context, msg);
        if (msgStr.IsError)
            return msgStr;

        if (basename is not null && haveLine)
            return PyStrObject.FromString($"{msgStr.Value.Value} ({basename}, line {MemberLong(linenoValue)})");
        if (basename is not null)
            return PyStrObject.FromString($"{msgStr.Value.Value} ({basename})");
        return PyStrObject.FromString($"{msgStr.Value.Value} (line {MemberLong(linenoValue)})");
    }

    private static long MemberLong(PyObject? value)
    {
        return value is PyIntObject pyInt ? (long)pyInt.Value : 0;
    }

    // CPython my_basename splits on the platform SEP only
    private static string Basename(string filename)
    {
        int separator = filename.LastIndexOf(Path.DirectorySeparatorChar);
        return separator >= 0 ? filename[(separator + 1)..] : filename;
    }

    // CPython SyntaxError_members: raw object members defaulting to None;
    // writes keep __str__ live since it reads the members each time
    [PyProperty("msg")]
    private static PyResult Get_Msg(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "msg");

    [PyProperty("msg", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Msg(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "msg", value);

    [PyProperty("filename")]
    private static PyResult Get_Filename(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "filename");

    [PyProperty("filename", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Filename(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "filename", value);

    [PyProperty("lineno")]
    private static PyResult Get_Lineno(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "lineno");

    [PyProperty("lineno", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Lineno(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "lineno", value);

    [PyProperty("offset")]
    private static PyResult Get_Offset(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "offset");

    [PyProperty("offset", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Offset(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "offset", value);

    [PyProperty("text")]
    private static PyResult Get_Text(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "text");

    [PyProperty("text", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Text(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "text", value);

    [PyProperty("end_lineno")]
    private static PyResult Get_EndLineno(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "end_lineno");

    [PyProperty("end_lineno", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_EndLineno(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "end_lineno", value);

    [PyProperty("end_offset")]
    private static PyResult Get_EndOffset(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "end_offset");

    [PyProperty("end_offset", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_EndOffset(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "end_offset", value);

    [PyProperty("print_file_and_line")]
    private static PyResult Get_PrintFileAndLine(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "print_file_and_line");

    [PyProperty("print_file_and_line", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_PrintFileAndLine(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "print_file_and_line", value);

    [PyProperty("_metadata")]
    private static PyResult Get_Metadata(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "_metadata");

    [PyProperty("_metadata", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Metadata(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "_metadata", value);
}

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
public sealed partial class PyOSErrorObjectType : PyExceptionType
{
    private static Dictionary<long, PyExceptionType>? _errnoMap;

    // CPython _PyExc_InitState seeds the errnomap with the CRT errno
    // values of the build platform (MSVC on Windows). Built lazily so no
    // subclass type is constructed ahead of the runtime bootstrap order.
    private static Dictionary<long, PyExceptionType> ErrnoMap => _errnoMap ??= new Dictionary<long, PyExceptionType>
    {
        [1] = PyPermissionErrorObjectType.Shared,               // EPERM
        [2] = PyFileNotFoundErrorObjectType.Shared,             // ENOENT
        [3] = PyProcessLookupErrorObjectType.Shared,            // ESRCH
        [4] = PyInterruptedErrorObjectType.Shared,              // EINTR
        [10] = PyChildProcessErrorObjectType.Shared,            // ECHILD
        [11] = PyBlockingIOErrorObjectType.Shared,              // EAGAIN
        [13] = PyPermissionErrorObjectType.Shared,              // EACCES
        [17] = PyFileExistsErrorObjectType.Shared,              // EEXIST
        [20] = PyNotADirectoryErrorObjectType.Shared,           // ENOTDIR
        [21] = PyIsADirectoryErrorObjectType.Shared,            // EISDIR
        [32] = PyBrokenPipeErrorObjectType.Shared,              // EPIPE
        [10035] = PyBlockingIOErrorObjectType.Shared,           // EWOULDBLOCK
        [10036] = PyBlockingIOErrorObjectType.Shared,           // EINPROGRESS
        [10037] = PyBlockingIOErrorObjectType.Shared,           // EALREADY
        [10053] = PyConnectionAbortedErrorObjectType.Shared,    // ECONNABORTED
        [10054] = PyConnectionResetErrorObjectType.Shared,      // ECONNRESET
        [10058] = PyBrokenPipeErrorObjectType.Shared,           // ESHUTDOWN
        [10060] = PyTimeoutErrorObjectType.Shared,              // ETIMEDOUT / WSAETIMEDOUT
        [10061] = PyConnectionRefusedErrorObjectType.Shared,    // ECONNREFUSED
    };

    // CPython OSError_new: keywords are rejected for the exact OSError
    // type; a Python subclass without its own __init__ reaches the same
    // rejection through the inherited Init. A 2..5-argument call whose
    // first argument is a mapped errno swaps the allocated type before
    // construction. CPython additionally maps a winerror back to a CRT
    // errno through PC/errmap.h — PySharp does not model that remap.
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0 && ReferenceEquals(cls, Shared))
            return PyResult.TypeError(PySR.Runtime_Exception_TakesNoKeywordArguments, cls.Name);

        if (ReferenceEquals(cls, Shared) &&
            args.Count is >= 2 and <= 5 &&
            args[0] is PyIntObject errno &&
            ErrnoMap.TryGetValue((long)errno.Value, out var mapped))
            cls = mapped;

        return new PyExceptionObject(cls, [.. args]);
    }

    // CPython oserror_parse_args/oserror_init: only 2..5 positional
    // arguments are interpreted; a non-None filename (and filename2) is
    // stored as a member and args is truncated to (errno, strerror) for
    // the legacy in-place unpacking compatibility. The BlockingIOError
    // characters_written special case is not modelled — its numeric third
    // argument is stored as filename like any other OSError.
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Exception_TakesNoKeywordArguments, self.PyType.Name);

        self.Args = [.. args];
        if (args.Count is < 2 or > 5)
            return PyNoneObject.None;

        var errno = args[0];
        var strerror = args[1];
        PyObject? filename = args.Count >= 3 ? args[2] : null;
        PyObject? winerror = args.Count >= 4 ? args[3] : null;
        PyObject? filename2 = args.Count >= 5 ? args[4] : null;

        self.PyAttributes["errno"] = errno;
        self.PyAttributes["strerror"] = strerror;

        if (filename is not null && filename is not PyNoneObject)
        {
            self.PyAttributes["filename"] = filename;
            if (filename2 is not null && filename2 is not PyNoneObject)
                self.PyAttributes["filename2"] = filename2;

            self.Args = [errno, strerror];
        }

        if (winerror is not null)
            self.PyAttributes["winerror"] = winerror;

        return PyNoneObject.None;
    }

    // CPython OSError_str (MS_WINDOWS build): winerror takes priority
    // over errno; errno/strerror/winerror render through str() and the
    // filenames through repr().
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        var attrs = self.PyAttributes;
        PyObject? winerror = attrs.TryGetValue("winerror", out var winerrorValue) ? winerrorValue : null;
        PyObject? strerror = attrs.TryGetValue("strerror", out var strerrorValue) ? strerrorValue : null;

        if (winerror is not null)
        {
            if (attrs.TryGetValue("filename", out var filenameValue))
            {
                PyObject? filename2 = attrs.TryGetValue("filename2", out var filename2Value) ? filename2Value : null;
                return OSErrorMessage(context, winerror, strerror, filenameValue, filename2, winerror: true);
            }
            if (strerror is not null)
                return OSErrorMessage(context, winerror, strerror, null, null, winerror: true);
        }

        if (attrs.TryGetValue("filename", out var filename))
        {
            attrs.TryGetValue("errno", out var errnoValue);
            attrs.TryGetValue("filename2", out var filename2);
            return OSErrorMessage(context, errnoValue, strerror, filename, filename2, winerror: false);
        }

        if (attrs.TryGetValue("errno", out var errno) && strerror is not null)
            return OSErrorMessage(context, errno, strerror, null, null, winerror: false);

        return PyBaseExceptionObjectType.BaseExceptionStr(context, self);
    }

    private static PyResult OSErrorMessage(PyCallContext context, PyObject? first, PyObject? second, PyObject? filename, PyObject? filename2, bool winerror)
    {
        var builder = new StringBuilder();
        builder.Append(winerror ? "[WinError " : "[Errno ");

        var firstStr = PySpecialMethods.Str(context, first ?? PyNoneObject.None);
        if (firstStr.IsError)
            return firstStr;
        builder.Append(firstStr.Value.Value);

        builder.Append("] ");
        if (second is not null)
        {
            var secondStr = PySpecialMethods.Str(context, second);
            if (secondStr.IsError)
                return secondStr;
            builder.Append(secondStr.Value.Value);
        }
        else
        {
            builder.Append("None");
        }

        if (filename is not null)
        {
            var filenameRepr = PySpecialMethods.Repr(context, filename);
            if (filenameRepr.IsError)
                return filenameRepr;
            builder.Append(": ").Append(filenameRepr.Value.Value);

            if (filename2 is not null)
            {
                var filename2Repr = PySpecialMethods.Repr(context, filename2);
                if (filename2Repr.IsError)
                    return filename2Repr;
                builder.Append(" -> ").Append(filename2Repr.Value.Value);
            }
        }

        return PyStrObject.FromString(builder.ToString());
    }

    // CPython OSError_members: raw object members defaulting to None
    // (winerror exists in MS_WINDOWS builds only, which PySharp mirrors)
    [PyProperty("errno")]
    private static PyResult Get_Errno(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "errno");

    [PyProperty("errno", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Errno(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "errno", value);

    [PyProperty("strerror")]
    private static PyResult Get_Strerror(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "strerror");

    [PyProperty("strerror", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Strerror(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "strerror", value);

    [PyProperty("filename")]
    private static PyResult Get_Filename(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "filename");

    [PyProperty("filename", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Filename(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "filename", value);

    [PyProperty("filename2")]
    private static PyResult Get_Filename2(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "filename2");

    [PyProperty("filename2", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Filename2(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "filename2", value);

    [PyProperty("winerror")]
    private static PyResult Get_Winerror(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "winerror");

    [PyProperty("winerror", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Winerror(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "winerror", value);
}

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
            return PyResult.TypeError($"function takes exactly 5 arguments ({args.Count} given)");

        if (args[0] is not PyStrObject)
            return PyResult.TypeError($"argument 1 must be str, not {args[0].PyType.Name}");
        if (args[1] is not PyBytesObject and not PyByteArrayObject and not PyMemoryViewObject)
            return PyResult.TypeError($"a bytes-like object is required, not '{args[1].PyType.Name}'");
        if (args[2] is not PyIntObject)
            return PyResult.TypeError($"'{args[2].PyType.Name}' object cannot be interpreted as an integer");
        if (args[3] is not PyIntObject)
            return PyResult.TypeError($"'{args[3].PyType.Name}' object cannot be interpreted as an integer");
        if (args[4] is not PyStrObject)
            return PyResult.TypeError($"argument 5 must be str, not {args[4].PyType.Name}");

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
            PyMemoryViewObject memoryView => memoryView.DataSpan,
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
public sealed partial class PyUnicodeTranslateErrorObjectType : PyExceptionType
{
    // CPython UnicodeTranslateError_init: the "UnnU" parse — object, two
    // index integers, reason — and no encoding member
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Exception_TakesNoKeywordArguments, self.PyType.Name);

        self.Args = [.. args];

        if (args.Count is not 4)
            return PyResult.TypeError($"function takes exactly 4 arguments ({args.Count} given)");
        if (args[0] is not PyStrObject)
            return PyResult.TypeError($"argument 1 must be str, not {args[0].PyType.Name}");
        if (args[1] is not PyIntObject)
            return PyResult.TypeError($"'{args[1].PyType.Name}' object cannot be interpreted as an integer");
        if (args[2] is not PyIntObject)
            return PyResult.TypeError($"'{args[2].PyType.Name}' object cannot be interpreted as an integer");
        if (args[3] is not PyStrObject)
            return PyResult.TypeError($"argument 4 must be str, not {args[3].PyType.Name}");

        self.PyAttributes["object"] = args[0];
        self.PyAttributes["start"] = args[1];
        self.PyAttributes["end"] = args[2];
        self.PyAttributes["reason"] = args[3];
        return PyNoneObject.None;
    }

    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        return PyUnicodeErrorStr.Format(context, self, withEncoding: false);
    }
}

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

#region Shared Exception Member Helpers

// CPython exposes exception members through PyMemberDef getsets that
// read as None until __init__ fills them; the OSError and SyntaxError
// member tables share these accessors over the instance attributes.
internal static class PyExceptionMemberAccess
{
    internal static PyResult Get(PyExceptionObject self, string name)
    {
        return self.PyAttributes.TryGetValue(name, out var value) ? value : PyNoneObject.None;
    }

    internal static PyResult Set(PyExceptionObject self, string name, PyObject value)
    {
        self.PyAttributes[name] = value;
        return PyNoneObject.None;
    }
}

// Objects/exceptions.c UnicodeEncodeError_str / UnicodeTranslateError_str:
// a single bad code point renders with its escape form (\x, \u, \U by
// magnitude), anything else as a start-(end-1) range. Encoding and
// reason are re-str()'d so post-construction member writes stay live.
internal static class PyUnicodeErrorStr
{
    internal static PyResult Format(PyCallContext context, PyExceptionObject self, bool withEncoding)
    {
        var attrs = self.PyAttributes;
        if (!attrs.TryGetValue("object", out var objectAttr) ||
            !attrs.TryGetValue("start", out var startAttr) ||
            !attrs.TryGetValue("end", out var endAttr) ||
            !attrs.TryGetValue("reason", out var reasonAttr))
            return PyStrObject.Empty;

        if (objectAttr is not PyStrObject objectStr)
            return PyStrObject.Empty;

        string codec = string.Empty;
        if (withEncoding)
        {
            if (!attrs.TryGetValue("encoding", out var encodingAttr))
                return PyStrObject.Empty;
            var encodingStr = PySpecialMethods.Str(context, encodingAttr);
            if (encodingStr.IsError)
                return encodingStr;
            codec = $"'{encodingStr.Value.Value}' codec ";
        }

        var reasonStr = PySpecialMethods.Str(context, reasonAttr);
        if (reasonStr.IsError)
            return reasonStr;

        long length = objectStr.PyLength;
        long start = ReadMemberIndex(startAttr);
        long end = ReadMemberIndex(endAttr);
        string verb = withEncoding ? "encode" : "translate";

        if (start >= 0 && start < length && end >= 0 && end <= length && end == start + 1)
        {
            // PyUnicode_ReadChar returns the raw code point, lone
            // surrogates included
            uint badchar = (uint)PyStrObject.CodePointAt(objectStr.Value, (int)start);

            string escape = badchar <= 0xff ? $"\\x{badchar:x2}"
                : badchar <= 0xffff ? $"\\u{badchar:x4}" : $"\\U{badchar:x8}";
            return PyStrObject.FromString(
                $"{codec}can't {verb} character '{escape}' in position {start}: {reasonStr.Value.Value}");
        }
        return PyStrObject.FromString(
            $"{codec}can't {verb} characters in position {start}-{end - 1}: {reasonStr.Value.Value}");
    }

    private static long ReadMemberIndex(PyObject value)
    {
        return value is PyIntObject pyInt ? (long)pyInt.Value : 0;
    }
}

#endregion
