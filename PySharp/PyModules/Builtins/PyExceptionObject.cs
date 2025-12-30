using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Metadata;
using System.Diagnostics;
using System.Text;

namespace PySharp.PyModules.Builtins;

public sealed class PyExceptionObject : PyObject
{
    public override PyTypeObject DefaultPyType { get; }

    internal PyExceptionObject(PyTypeObject exceptionType, params IEnumerable<PyObject> args)
    {
        Debug.Assert(exceptionType.IsSubclassOf(PyBaseExceptionObjectType.Shared));

        DefaultPyType = exceptionType;
        Args = [.. args];
    }

    public bool SuppressContext { get; internal set; }
    public PyExceptionObject? Cause { get; internal set; }
    public PyExceptionObject? Context { get; internal set; }
    public string? CauseReason { get; internal set; }
    public IReadOnlyList<PyObject> Args { get; }
    public string? Traceback { get; internal set; }
    internal string? ThreadTracebackInfo { get; set; }

    internal PyExceptionObject WithTraceback(PyCallContext context, bool overwriteExisting = false)
    {
        if (Traceback is not null && !overwriteExisting)
            return this;

        Traceback = PyTraceback.PrintTraceback(context);
        var frame = context.CurrentFrame;
        while (frame is not null)
        {
            var back = frame.Back;
            if (back is not null && back.FrameType is FrameType.ThreadRoot)
            {
                Debug.Assert(back.IsRoot);
                ThreadTracebackInfo = $"Exception in thread Thread-{Environment.CurrentManagedThreadId} ({frame.CallerName}):";
                break;
            }
            frame = back;
        }

        return this;
    }

    internal string ToMessage(PyCallContext context)
    {
        var builder = new StringBuilder();

        if (Cause is not null)
            builder
                .AppendLine(Cause.ToMessage(context))
                .AppendLine()
                .AppendLine(CauseReason)
                .AppendLine();
        else if (!SuppressContext && Context is not null)
            builder
                .AppendLine(Context.ToMessage(context))
                .AppendLine()
                .AppendLine("During handling of the above exception, another exception occurred:")
                .AppendLine();

        if (ThreadTracebackInfo is not null)
        {
            builder
                .AppendLine(ThreadTracebackInfo);
        }

        if (Traceback is not null)
        {
            builder
                .AppendLine("Traceback (most recent call last):")
                .Append(Traceback);
        }

        builder.Append(PyType.FullName);
        if (PySpecialMethods.TryGetStr(context, this, out var s, out var result))
        {
            if (s.Value != string.Empty)
                builder.Append(": ").Append(s.Value);
        }
        else
        {
            builder.Append(": ").Append("<exception str() failed>");
        }

        return builder.ToString();
    }
}

public abstract class PyExceptionType : PyTypeObject<PyExceptionObject>
{
    public PyExceptionObject Create()
    {
        return new PyExceptionObject(this);
    }

    public PyExceptionObject Create(PyObject? pyObject)
    {
        return new PyExceptionObject(this, pyObject is null ? [] : [pyObject]);
    }

    public PyExceptionObject Create(params IEnumerable<PyObject> pyObjects)
    {
        return new PyExceptionObject(this, [.. pyObjects]);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.RaiseTypeError(null);

        return new PyExceptionObject(cls, [.. args]);
    }

    protected internal override PyResult Repr(PyCallContext context, PyExceptionObject self)
    {
        var builder = new StringBuilder();
        builder.Append(PyType.Name);
        builder.Append('(');

        for (int i = 0; i < self.Args.Count; i++)
        {
            if (!PySpecialMethods.TryGetRepr(context, self.Args[i], out var s, out var result))
                return result;

            if (i > 0)
                builder.Append(", ");
            builder.Append(s);
        }

        builder.Append(')');

        return PyStrObject.FromString(builder.ToString());
    }

    protected internal override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        if (self.Args.Count is 0)
            return PyStrObject.Empty;

        if (self.Args.Count is 1)
            return self.Args[0].Str(context);

        return PyTupleObject.CreateTuple(self.Args).Str(context);
    }
}

public abstract class PyExceptionType<TSelf> : PyExceptionType, ISharedInstance<TSelf> where TSelf : PyExceptionType<TSelf>, ISharedInstance<TSelf>, new()
{
    public static TSelf Shared { get; } = new TSelf();
}

public abstract class PyExceptionType<TSelf, TBase> : PyExceptionType<TSelf>, ISharedInstance<TSelf>
    where TSelf : PyExceptionType<TSelf, TBase>, ISharedInstance<TSelf>, new()
    where TBase : PyExceptionType<TBase>, ISharedInstance<TBase>, new()
{
    public sealed override IReadOnlyList<PyTypeObject> Bases => [TBase.Shared];
}

#region Base Classes

public sealed class PyBaseExceptionObjectType : PyExceptionType<PyBaseExceptionObjectType>
{
    public override string Name => "BaseException";
}

public sealed class PyExceptionObjectType : PyExceptionType<PyExceptionObjectType, PyBaseExceptionObjectType>
{
    public override string Name => "Exception";
}

public sealed class PyLookupErrorObjectType : PyExceptionType<PyLookupErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "LookupError";
}

public sealed class PyArithmeticErrorObjectType : PyExceptionType<PyArithmeticErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "ArithmeticError";
}

#endregion Base Classes

#region Concrete Exceptions

public sealed class PySystemExitObjectType : PyExceptionType<PySystemExitObjectType, PyBaseExceptionObjectType>
{
    public override string Name => "SystemExit";
}

public sealed class PyGeneratorExitObjectType : PyExceptionType<PyGeneratorExitObjectType, PyBaseExceptionObjectType>
{
    public override string Name => "GeneratorExit";
}

public sealed class PyTypeErrorObjectType : PyExceptionType<PyTypeErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "TypeError";
}

public sealed class PyStopIterationObjectType : PyExceptionType<PyStopIterationObjectType, PyExceptionObjectType>
{
    public override string Name => "StopIteration";
}

public sealed class PyAttributeErrorObjectType : PyExceptionType<PyAttributeErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "AttributeError";
}

public sealed class PyKeyErrorObjectType : PyExceptionType<PyKeyErrorObjectType, PyLookupErrorObjectType>
{
    public override string Name => "KeyError";
}

public sealed class PyIndexErrorObjectType : PyExceptionType<PyIndexErrorObjectType, PyLookupErrorObjectType>
{
    public override string Name => "IndexError";
}

public sealed class PyValueErrorObjectType : PyExceptionType<PyValueErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "ValueError";
}

public sealed class PyNameErrorObjectType : PyExceptionType<PyNameErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "NameError";
}

public sealed class PyUnboundLocalErrorObjectType : PyExceptionType<PyUnboundLocalErrorObjectType, PyNameErrorObjectType>
{
    public override string Name => "UnboundLocalError";
}

public sealed class PyImportErrorObjectType : PyExceptionType<PyImportErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "ImportError";
}

public sealed class PyModuleNotFoundErrorObjectType : PyExceptionType<PyModuleNotFoundErrorObjectType, PyImportErrorObjectType>
{
    public override string Name => "ModuleNotFoundError";
}

public sealed class PySyntaxErrorObjectType : PyExceptionType<PySyntaxErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "SyntaxError";
}

public sealed class PyIndentationErrorObjectType : PyExceptionType<PyIndentationErrorObjectType, PySyntaxErrorObjectType>
{
    public override string Name => "IndentationError";
}

public sealed class PyZeroDivisionErrorObjectType : PyExceptionType<PyZeroDivisionErrorObjectType, PyArithmeticErrorObjectType>
{
    public override string Name => "ZeroDivisionError";
}

public sealed class PyAssertionErrorObjectType : PyExceptionType<PyAssertionErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "AssertionError";
}

public sealed class PyRuntimeErrorObjectType : PyExceptionType<PyRuntimeErrorObjectType, PyExceptionObjectType>
{
    public override string Name => "RuntimeError";
}

#endregion Concrete Exceptions

#region Warnings

public sealed class PyWarningObjectType : PyExceptionType<PyWarningObjectType, PyExceptionObjectType>
{
    public override string Name => "Warning";
}

public sealed class PyUserWarningObjectType : PyExceptionType<PyUserWarningObjectType, PyWarningObjectType>
{
    public override string Name => "UserWarning";
}

public sealed class PySyntaxWarningObjectType : PyExceptionType<PySyntaxWarningObjectType, PyWarningObjectType>
{
    public override string Name => "SyntaxWarning";
}

public sealed class PyDeprecationWarningObjectType : PyExceptionType<PyDeprecationWarningObjectType, PyWarningObjectType>
{
    public override string Name => "DeprecationWarning";
}

#endregion

public static class PyStandardExceptionTypes
{
    public static readonly PyBaseExceptionObjectType BaseException = PyBaseExceptionObjectType.Shared;
    public static readonly PySystemExitObjectType SystemExit = PySystemExitObjectType.Shared;
    public static readonly PyExceptionObjectType Exception = PyExceptionObjectType.Shared;
    public static readonly PyTypeErrorObjectType TypeError = PyTypeErrorObjectType.Shared;
    public static readonly PyStopIterationObjectType StopIteration = PyStopIterationObjectType.Shared;
    public static readonly PyAttributeErrorObjectType AttributeError = PyAttributeErrorObjectType.Shared;
    public static readonly PyLookupErrorObjectType LookupError = PyLookupErrorObjectType.Shared;
    public static readonly PyKeyErrorObjectType KeyError = PyKeyErrorObjectType.Shared;
    public static readonly PyIndexErrorObjectType IndexError = PyIndexErrorObjectType.Shared;
    public static readonly PyValueErrorObjectType ValueError = PyValueErrorObjectType.Shared;
    public static readonly PyNameErrorObjectType NameError = PyNameErrorObjectType.Shared;
    public static readonly PyImportErrorObjectType ImportError = PyImportErrorObjectType.Shared;
    public static readonly PyModuleNotFoundErrorObjectType ModuleNotFoundError = PyModuleNotFoundErrorObjectType.Shared;
    public static readonly PySyntaxErrorObjectType SyntaxError = PySyntaxErrorObjectType.Shared;
    public static readonly PyIndentationErrorObjectType IndentationError = PyIndentationErrorObjectType.Shared;
    public static readonly PyArithmeticErrorObjectType ArithmeticError = PyArithmeticErrorObjectType.Shared;
    public static readonly PyZeroDivisionErrorObjectType ZeroDivisionError = PyZeroDivisionErrorObjectType.Shared;
    public static readonly PyAssertionErrorObjectType AssertionError = PyAssertionErrorObjectType.Shared;
    public static readonly PyUnboundLocalErrorObjectType UnboundLocalError = PyUnboundLocalErrorObjectType.Shared;
    public static readonly PyRuntimeErrorObjectType RuntimeError = PyRuntimeErrorObjectType.Shared;
    public static readonly PyGeneratorExitObjectType GeneratorExit = PyGeneratorExitObjectType.Shared;
}