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

    // CPython BaseException___traceback___get_impl returns self->traceback,
    // which is NULL until the exception is raised (or assigned through
    // with_traceback / the setter)
    [PyProperty(PySpecialNames.Traceback)]
    private static PyResult Get_Traceback(PyCallContext context, PyExceptionObject self)
    {
        return (PyObject?)self.Traceback ?? PyNoneObject.None;
    }

    // CPython BaseException___traceback___set_impl accepts a traceback or
    // None (clearing it) and rejects anything else
    [PyProperty(PySpecialNames.Traceback, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Traceback(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        if (value is PyNoneObject)
        {
            self.Traceback = null;
            return PyNoneObject.None;
        }

        if (value is not PyTracebackObject traceback)
            return PyResult.TypeError(PySR.Runtime_BaseException_TracebackMustBeTracebackOrNone);

        self.Traceback = traceback;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Traceback, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Traceback(PyCallContext context, PyExceptionObject self)
    {
        return PyResult.TypeError(PySR.Runtime_BaseException_TracebackMayNotBeDeleted);
    }

    // CPython BaseException_with_traceback delegates to the __traceback__
    // setter and returns self; it leaves __cause__/__context__ and
    // __suppress_context__ untouched.
    [PyMethod("with_traceback")]
    [PyFunctionParameters("tb", "/")]
    private static PyResult WithTraceback(PyCallContext context, PyExceptionObject self, PyArguments arguments)
    {
        var setResult = Set_Traceback(context, self, arguments[0]);
        if (setResult.IsError)
            return setResult;

        return self;
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
        self.SetMember("code", args.Count switch
        {
            0 => PyNoneObject.None,
            1 => args[0],
            _ => PyTupleObject.CreateTuple(args),
        });
        return PyNoneObject.None;
    }

    [PyProperty("code")]
    private static PyResult Get_Code(PyCallContext context, PyExceptionObject self)
    {
        return self.GetMember("code") ?? PyNoneObject.None;
    }

    [PyProperty("code", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Code(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        self.SetMember("code", value);
        return PyNoneObject.None;
    }

    [PyProperty("code", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Code(PyCallContext context, PyExceptionObject self)
    {
        self.DeleteMember("code");
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

        self.SetMember("value", args.Count > 0 ? args[0] : PyNoneObject.None);
        return PyNoneObject.None;
    }

    [PyProperty("value")]
    private static PyResult Get_Value(PyCallContext context, PyExceptionObject self)
    {
        return self.GetMember("value") ?? PyNoneObject.None;
    }

    [PyProperty("value", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Value(PyCallContext context, PyExceptionObject self, PyObject value)
    {
        self.SetMember("value", value);
        return PyNoneObject.None;
    }

    [PyProperty("value", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Value(PyCallContext context, PyExceptionObject self)
    {
        self.DeleteMember("value");
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
    // as str and start/end as index integers (the "n" format consults
    // __index__ and range checks against ssize_t).
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

        var startResult = PyExceptionMemberAccess.ParseSsize(context, args[2]);
        if (startResult.IsError)
            return startResult;
        var endResult = PyExceptionMemberAccess.ParseSsize(context, args[3]);
        if (endResult.IsError)
            return endResult;

        if (args[4] is not PyStrObject)
            return PyResult.TypeError($"argument 5 must be str, not {args[4].PyType.Name}");

        self.SetMember("encoding", args[0]);
        self.SetMember("object", args[1]);
        self.SetMember("start", startResult.Value);
        self.SetMember("end", endResult.Value);
        self.SetMember("reason", args[4]);
        return PyNoneObject.None;
    }

    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        return PyUnicodeErrorStr.Format(context, self, withEncoding: true);
    }

    [PyProperty("encoding")]
    private static PyResult Get_Encoding(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "encoding");

    [PyProperty("encoding", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Encoding(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "encoding", value);

    [PyProperty("encoding", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Encoding(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "encoding");

    [PyProperty("object")]
    private static PyResult Get_Object(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "object");

    [PyProperty("object", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Object(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "object", value);

    [PyProperty("object", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Object(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "object");

    [PyProperty("start")]
    private static PyResult Get_Start(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "start");

    [PyProperty("start", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Start(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.SetSsize(self, "start", value);

    [PyProperty("start", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Start(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.DeleteNumeric();

    [PyProperty("end")]
    private static PyResult Get_End(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "end");

    [PyProperty("end", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_End(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.SetSsize(self, "end", value);

    [PyProperty("end", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_End(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.DeleteNumeric();

    [PyProperty("reason")]
    private static PyResult Get_Reason(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "reason");

    [PyProperty("reason", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Reason(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "reason", value);

    [PyProperty("reason", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Reason(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "reason");
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

            self.SetMember(key, value);
        }

        if (args.Count is 1)
            self.SetMember("msg", args[0]);

        return PyNoneObject.None;
    }

    // CPython ImportError_str: an exact str msg wins over the args rendering,
    // so a one-argument str() is the message even when args carries more
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        if (self.GetMember("msg") is { PyType: PyStrObjectType } msg)
            return PySpecialMethods.Str(context, msg);

        return PyBaseExceptionObjectType.BaseExceptionStr(context, self);
    }

    // The rest are plain members (Objects/exceptions.c ImportError_members):
    // assignment and deletion go through the member slot and are never
    // rejected, and an unset member reads back as None.
    [PyProperty("msg")]
    private static PyResult Get_Msg(PyCallContext context, PyExceptionObject self)
        => PyExceptionMemberAccess.Get(self, "msg");

    [PyProperty("msg", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Msg(PyCallContext context, PyExceptionObject self, PyObject value)
        => PyExceptionMemberAccess.Set(self, "msg", value);

    [PyProperty("msg", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Msg(PyCallContext context, PyExceptionObject self)
        => PyExceptionMemberAccess.Delete(self, "msg");

    [PyProperty("name")]
    private static PyResult Get_Name(PyCallContext context, PyExceptionObject self)
        => PyExceptionMemberAccess.Get(self, "name");

    [PyProperty("name", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Name(PyCallContext context, PyExceptionObject self, PyObject value)
        => PyExceptionMemberAccess.Set(self, "name", value);

    [PyProperty("name", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Name(PyCallContext context, PyExceptionObject self)
        => PyExceptionMemberAccess.Delete(self, "name");

    [PyProperty("path")]
    private static PyResult Get_Path(PyCallContext context, PyExceptionObject self)
        => PyExceptionMemberAccess.Get(self, "path");

    [PyProperty("path", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Path(PyCallContext context, PyExceptionObject self, PyObject value)
        => PyExceptionMemberAccess.Set(self, "path", value);

    [PyProperty("path", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Path(PyCallContext context, PyExceptionObject self)
        => PyExceptionMemberAccess.Delete(self, "path");

    [PyProperty("name_from")]
    private static PyResult Get_NameFrom(PyCallContext context, PyExceptionObject self)
        => PyExceptionMemberAccess.Get(self, "name_from");

    [PyProperty("name_from", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_NameFrom(PyCallContext context, PyExceptionObject self, PyObject value)
        => PyExceptionMemberAccess.Set(self, "name_from", value);

    [PyProperty("name_from", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_NameFrom(PyCallContext context, PyExceptionObject self)
        => PyExceptionMemberAccess.Delete(self, "name_from");
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
            self.SetMember("msg", args[0]);

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

        self.SetMember("filename", info[0]);
        self.SetMember("lineno", info[1]);
        self.SetMember("offset", info[2]);
        self.SetMember("text", info[3]);

        if (info.Count is 5)
            return PyResult.TypeError("end_offset must be provided when end_lineno is provided");
        if (info.Count >= 6)
        {
            self.SetMember("end_lineno", info[4]);
            self.SetMember("end_offset", info[5]);
        }
        if (info.Count is 7)
            self.SetMember("_metadata", info[6]);

        return PyNoneObject.None;
    }

    // CPython SyntaxError_str: the filename only renders when it is a str,
    // reduced to its basename on the platform separator; the line number
    // only counts when it is an exact int. Without either, the message
    // degenerates to str(msg) — None included.
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        PyObject msg = self.GetMember("msg") ?? PyNoneObject.None;

        string? basename = null;
        if (self.GetMember("filename") is PyStrObject filenameStr)
            basename = Basename(filenameStr.Value);

        var linenoValue = self.GetMember("lineno");
        bool haveLine = linenoValue is PyIntObject and not PyBoolObject;

        if (basename is null && !haveLine)
            return PySpecialMethods.Str(context, msg);

        var msgStr = PySpecialMethods.Str(context, msg);
        if (msgStr.IsError)
            return msgStr;

        if (basename is not null && haveLine)
            return PyStrObject.FromString($"{msgStr.Value.Value} ({basename}, line {PyExceptionMemberAccess.MemberLong(linenoValue)})");
        if (basename is not null)
            return PyStrObject.FromString($"{msgStr.Value.Value} ({basename})");
        return PyStrObject.FromString($"{msgStr.Value.Value} (line {PyExceptionMemberAccess.MemberLong(linenoValue)})");
    }

    // CPython my_basename splits on the platform SEP only
    private static string Basename(string filename)
    {
        int separator = filename.LastIndexOf(Path.DirectorySeparatorChar);
        return separator >= 0 ? filename[(separator + 1)..] : filename;
    }

    // CPython SyntaxError_members: raw object members defaulting to None;
    // writes keep __str__ live since it reads the members each time.
    // Every one of them is deletable (Py_T_OBJECT, flags 0).
    [PyProperty("msg")]
    private static PyResult Get_Msg(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "msg");

    [PyProperty("msg", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Msg(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "msg", value);

    [PyProperty("msg", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Msg(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "msg");

    [PyProperty("filename")]
    private static PyResult Get_Filename(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "filename");

    [PyProperty("filename", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Filename(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "filename", value);

    [PyProperty("filename", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Filename(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "filename");

    [PyProperty("lineno")]
    private static PyResult Get_Lineno(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "lineno");

    [PyProperty("lineno", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Lineno(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "lineno", value);

    [PyProperty("lineno", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Lineno(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "lineno");

    [PyProperty("offset")]
    private static PyResult Get_Offset(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "offset");

    [PyProperty("offset", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Offset(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "offset", value);

    [PyProperty("offset", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Offset(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "offset");

    [PyProperty("text")]
    private static PyResult Get_Text(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "text");

    [PyProperty("text", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Text(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "text", value);

    [PyProperty("text", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Text(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "text");

    [PyProperty("end_lineno")]
    private static PyResult Get_EndLineno(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "end_lineno");

    [PyProperty("end_lineno", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_EndLineno(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "end_lineno", value);

    [PyProperty("end_lineno", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_EndLineno(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "end_lineno");

    [PyProperty("end_offset")]
    private static PyResult Get_EndOffset(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "end_offset");

    [PyProperty("end_offset", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_EndOffset(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "end_offset", value);

    [PyProperty("end_offset", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_EndOffset(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "end_offset");

    [PyProperty("print_file_and_line")]
    private static PyResult Get_PrintFileAndLine(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "print_file_and_line");

    [PyProperty("print_file_and_line", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_PrintFileAndLine(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "print_file_and_line", value);

    [PyProperty("print_file_and_line", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_PrintFileAndLine(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "print_file_and_line");

    [PyProperty("_metadata")]
    private static PyResult Get_Metadata(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "_metadata");

    [PyProperty("_metadata", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Metadata(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "_metadata", value);

    [PyProperty("_metadata", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Metadata(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "_metadata");
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

        self.SetMember("errno", errno);
        self.SetMember("strerror", strerror);

        if (filename is not null && filename is not PyNoneObject)
        {
            self.SetMember("filename", filename);
            if (filename2 is not null && filename2 is not PyNoneObject)
                self.SetMember("filename2", filename2);

            self.Args = [errno, strerror];
        }

        if (winerror is not null)
            self.SetMember("winerror", winerror);

        return PyNoneObject.None;
    }

    // CPython OSError_str (MS_WINDOWS build): winerror takes priority
    // over errno; errno/strerror/winerror render through str() and the
    // filenames through repr().
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        PyObject? winerror = self.GetMember("winerror");
        PyObject? strerror = self.GetMember("strerror");

        if (winerror is not null)
        {
            if (self.GetMember("filename") is { } filenameValue)
            {
                PyObject? filename2 = self.GetMember("filename2");
                return OSErrorMessage(context, winerror, strerror, filenameValue, filename2, winerror: true);
            }
            if (strerror is not null)
                return OSErrorMessage(context, winerror, strerror, null, null, winerror: true);
        }

        if (self.GetMember("filename") is { } filename)
        {
            PyObject? errnoValue = self.GetMember("errno");
            PyObject? filename2 = self.GetMember("filename2");
            return OSErrorMessage(context, errnoValue, strerror, filename, filename2, winerror: false);
        }

        if (self.GetMember("errno") is { } errno && strerror is not null)
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

    [PyProperty("errno", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Errno(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "errno");

    [PyProperty("strerror")]
    private static PyResult Get_Strerror(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "strerror");

    [PyProperty("strerror", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Strerror(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "strerror", value);

    [PyProperty("strerror", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Strerror(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "strerror");

    [PyProperty("filename")]
    private static PyResult Get_Filename(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "filename");

    [PyProperty("filename", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Filename(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "filename", value);

    [PyProperty("filename", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Filename(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "filename");

    [PyProperty("filename2")]
    private static PyResult Get_Filename2(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "filename2");

    [PyProperty("filename2", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Filename2(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "filename2", value);

    [PyProperty("filename2", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Filename2(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "filename2");

    [PyProperty("winerror")]
    private static PyResult Get_Winerror(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "winerror");

    [PyProperty("winerror", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Winerror(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "winerror", value);

    [PyProperty("winerror", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Winerror(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "winerror");
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
    // is validated and stored so the attributes and str() stay in sync. The
    // "UOnnU" parse takes object as any buffer-providing value (CPython then
    // copies it into bytes) and start/end through __index__.
    protected override PyResult Init(PyCallContext context, PyExceptionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(PySR.Runtime_Exception_TakesNoKeywordArguments, self.PyType.Name);

        self.Args = [.. args];

        if (args.Count is not 5)
            return PyResult.TypeError($"function takes exactly 5 arguments ({args.Count} given)");

        if (args[0] is not PyStrObject)
            return PyResult.TypeError($"argument 1 must be str, not {args[0].PyType.Name}");
        if (args[1] is not PyBytesObject and not PyByteArrayObject and not PyMemoryViewObject)
            return PyResult.TypeError($"a bytes-like object is required, not '{args[1].PyType.Name}'");

        var startResult = PyExceptionMemberAccess.ParseSsize(context, args[2]);
        if (startResult.IsError)
            return startResult;
        var endResult = PyExceptionMemberAccess.ParseSsize(context, args[3]);
        if (endResult.IsError)
            return endResult;

        if (args[4] is not PyStrObject)
            return PyResult.TypeError($"argument 5 must be str, not {args[4].PyType.Name}");

        self.SetMember("encoding", args[0]);
        self.SetMember("object", args[1]);
        self.SetMember("start", startResult.Value);
        self.SetMember("end", endResult.Value);
        self.SetMember("reason", args[4]);
        return PyNoneObject.None;
    }

    // CPython UnicodeDecodeError_str: the object must be a bytes-like value
    // (checked at str() time too, so a post-construction `object = 5` raises
    // the fixed TypeError) and a single bad byte shows its value and position,
    // anything else a position range. Encoding and reason are re-str()'d.
    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        var objectAttr = self.GetMember("object");
        if (objectAttr is null)
            // CPython returns the empty string for an uninitialized object slot
            return PyStrObject.Empty;

        ReadOnlySpan<byte> data = objectAttr switch
        {
            PyBytesObject bytes => bytes.AsSpan(),
            PyByteArrayObject byteArray => byteArray.AsSpan(),
            PyMemoryViewObject memoryView => memoryView.DataSpan,
            _ => default,
        };
        if (objectAttr is not (PyBytesObject or PyByteArrayObject or PyMemoryViewObject))
            return PyResult.TypeError(PySR.Runtime_Unicode_ErrorObjectMustBeBytes);

        var reasonResult = PySpecialMethods.Str(context, self.GetMember("reason") ?? PyNoneObject.None);
        if (reasonResult.IsError)
            return reasonResult;
        var encodingResult = PySpecialMethods.Str(context, self.GetMember("encoding") ?? PyNoneObject.None);
        if (encodingResult.IsError)
            return encodingResult;

        long len = data.Length;
        long i = PyExceptionMemberAccess.MemberLong(self.GetMember("start"));
        long j = PyExceptionMemberAccess.MemberLong(self.GetMember("end"));

        if (i >= 0 && i < len && j >= 0 && j <= len && j == i + 1)
            return PyStrObject.FromString($"'{encodingResult.Value.Value}' codec can't decode byte 0x{data[(int)i]:x2} in position {i}: {reasonResult.Value.Value}");
        return PyStrObject.FromString($"'{encodingResult.Value.Value}' codec can't decode bytes in position {i}-{j - 1}: {reasonResult.Value.Value}");
    }

    [PyProperty("encoding")]
    private static PyResult Get_Encoding(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "encoding");

    [PyProperty("encoding", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Encoding(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "encoding", value);

    [PyProperty("encoding", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Encoding(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "encoding");

    [PyProperty("object")]
    private static PyResult Get_Object(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "object");

    [PyProperty("object", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Object(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "object", value);

    [PyProperty("object", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Object(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "object");

    [PyProperty("start")]
    private static PyResult Get_Start(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "start");

    [PyProperty("start", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Start(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.SetSsize(self, "start", value);

    [PyProperty("start", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Start(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.DeleteNumeric();

    [PyProperty("end")]
    private static PyResult Get_End(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "end");

    [PyProperty("end", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_End(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.SetSsize(self, "end", value);

    [PyProperty("end", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_End(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.DeleteNumeric();

    [PyProperty("reason")]
    private static PyResult Get_Reason(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "reason");

    [PyProperty("reason", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Reason(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "reason", value);

    [PyProperty("reason", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Reason(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "reason");
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

        var startResult = PyExceptionMemberAccess.ParseSsize(context, args[1]);
        if (startResult.IsError)
            return startResult;
        var endResult = PyExceptionMemberAccess.ParseSsize(context, args[2]);
        if (endResult.IsError)
            return endResult;

        if (args[3] is not PyStrObject)
            return PyResult.TypeError($"argument 4 must be str, not {args[3].PyType.Name}");

        self.SetMember("object", args[0]);
        self.SetMember("start", startResult.Value);
        self.SetMember("end", endResult.Value);
        self.SetMember("reason", args[3]);
        return PyNoneObject.None;
    }

    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        return PyUnicodeErrorStr.Format(context, self, withEncoding: false);
    }

    // UnicodeTranslateError shares UnicodeError_members with the decode/encode
    // variants, so it exposes encoding too — just never filled by __init__
    [PyProperty("encoding")]
    private static PyResult Get_Encoding(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "encoding");

    [PyProperty("encoding", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Encoding(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "encoding", value);

    [PyProperty("encoding", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Encoding(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "encoding");

    [PyProperty("object")]
    private static PyResult Get_Object(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "object");

    [PyProperty("object", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Object(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "object", value);

    [PyProperty("object", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Object(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "object");

    [PyProperty("start")]
    private static PyResult Get_Start(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "start");

    [PyProperty("start", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Start(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.SetSsize(self, "start", value);

    [PyProperty("start", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Start(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.DeleteNumeric();

    [PyProperty("end")]
    private static PyResult Get_End(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "end");

    [PyProperty("end", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_End(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.SetSsize(self, "end", value);

    [PyProperty("end", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_End(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.DeleteNumeric();

    [PyProperty("reason")]
    private static PyResult Get_Reason(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Get(self, "reason");

    [PyProperty("reason", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Reason(PyCallContext context, PyExceptionObject self, PyObject value) => PyExceptionMemberAccess.Set(self, "reason", value);

    [PyProperty("reason", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Reason(PyCallContext context, PyExceptionObject self) => PyExceptionMemberAccess.Delete(self, "reason");
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

// CPython exposes exception members through PyMemberDef getsets that read as
// None until __init__ fills them; the OSError, SyntaxError and UnicodeError
// member tables share these accessors. The value lives in PyExceptionObject's
// member map, not in the instance __dict__, so introspection stays clean.
internal static class PyExceptionMemberAccess
{
    // _Py_T_OBJECT members hold any object and read back as None when unset
    // (structmember.c PyMember_GetOne returns Py_None for a NULL slot).
    internal static PyResult Get(PyExceptionObject self, string name)
    {
        return self.GetMember(name) ?? PyNoneObject.None;
    }

    internal static PyResult Set(PyExceptionObject self, string name, PyObject value)
    {
        self.SetMember(name, value);
        return PyNoneObject.None;
    }

    internal static PyResult Delete(PyExceptionObject self, string name)
    {
        self.DeleteMember(name);
        return PyNoneObject.None;
    }

    // A Py_T_PYSSIZET slot is neither _Py_T_OBJECT nor _Py_T_OBJECT_EX, so
    // structmember.c PyMember_SetOne rejects the NULL write rather than
    // clearing it — Unicode*Error's start/end cannot be deleted.
    internal static PyResult DeleteNumeric()
    {
        return PyResult.TypeError(PySR.Runtime_Member_CannotDeleteNumeric);
    }

    // Py_T_PYSSIZET members store a C ssize_t, so the assigned value must go
    // through PyLong_AsSsize_t: an exact int (or bool/int subclass) is range
    // checked against the 64-bit ssize_t and saved as a plain int, anything
    // else — __index__ is NOT consulted — fails with structmember.c's fixed
    // "an integer is required" wording.
    internal static PyResult SetSsize(PyExceptionObject self, string name, PyObject value)
    {
        if (value is not PyIntObject pyInt)
            return PyResult.TypeError(PySR.Runtime_Member_IntegerRequired);

        if (pyInt.Value > long.MaxValue || pyInt.Value < long.MinValue)
            return PyResult.OverflowError(PySR.Runtime_Number_Int_TooLargeForSsize);

        self.SetMember(name, PyIntObject.FromInteger((long)pyInt.Value));
        return PyNoneObject.None;
    }

    // The "n" format of PyArg_ParseTuple (Python/getargs.c) does consult
    // __index__, so Unicode*Error.__init__ rejects a non-index value with the
    // generic index message and an out-of-ssize value with the overflow one.
    // The result is the normalized ssize_t as a plain int.
    internal static PyResult<PyIntObject> ParseSsize(PyCallContext context, PyObject value)
    {
        var indexResult = PySpecialMethods.Index(context, value);
        if (indexResult.IsError)
            return indexResult;

        var big = indexResult.Value.Value;
        if (big > long.MaxValue || big < long.MinValue)
            return PyResult.OverflowError(PySR.Runtime_Number_Int_TooLargeForSsize);

        return PyIntObject.FromInteger((long)big);
    }

    // A member rendered as a C long — an unset or non-int slot reads as 0,
    // matching the Py_ssize_t field's zero default when str() ignores the
    // member (CPython reads exc->start/end directly).
    internal static long MemberLong(PyObject? value)
        => value is PyIntObject pyInt ? (long)pyInt.Value : 0;
}

// Objects/exceptions.c UnicodeEncodeError_str / UnicodeTranslateError_str:
// a single bad code point renders with its escape form (\x, \u, \U by
// magnitude), anything else as a start-(end-1) range. Encoding and reason are
// re-str()'d so post-construction member writes stay live, and the object
// member is re-checked as a str, raising the fixed TypeError otherwise.
internal static class PyUnicodeErrorStr
{
    internal static PyResult Format(PyCallContext context, PyExceptionObject self, bool withEncoding)
    {
        var objectAttr = self.GetMember("object");
        if (objectAttr is null)
            // CPython returns the empty string for an uninitialized object slot
            return PyStrObject.Empty;

        var reasonStr = PySpecialMethods.Str(context, self.GetMember("reason") ?? PyNoneObject.None);
        if (reasonStr.IsError)
            return reasonStr;

        string codec = string.Empty;
        if (withEncoding)
        {
            var encodingStr = PySpecialMethods.Str(context, self.GetMember("encoding") ?? PyNoneObject.None);
            if (encodingStr.IsError)
                return encodingStr;
            codec = $"'{encodingStr.Value.Value}' codec ";
        }

        if (objectAttr is not PyStrObject objectStr)
            return PyResult.TypeError(PySR.Runtime_Unicode_ErrorObjectMustBeStr);

        long length = objectStr.PyLength;
        long start = PyExceptionMemberAccess.MemberLong(self.GetMember("start"));
        long end = PyExceptionMemberAccess.MemberLong(self.GetMember("end"));
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
}

#endregion
