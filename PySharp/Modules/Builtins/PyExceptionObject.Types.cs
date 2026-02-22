using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Text;

namespace PySharp.Modules.Builtins;

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

public sealed class PyBaseExceptionObjectType : PyExceptionType<PyBaseExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "BaseException";

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

public sealed class PyExceptionObjectType : PyExceptionType<PyExceptionObjectType, PyBaseExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "Exception";
}

public sealed class PyLookupErrorObjectType : PyExceptionType<PyLookupErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "LookupError";
}

public sealed class PyArithmeticErrorObjectType : PyExceptionType<PyArithmeticErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "ArithmeticError";
}

#endregion Base Classes

#region Concrete Exceptions

public sealed class PySystemExitObjectType : PyExceptionType<PySystemExitObjectType, PyBaseExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "SystemExit";
}

public sealed class PyGeneratorExitObjectType : PyExceptionType<PyGeneratorExitObjectType, PyBaseExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "GeneratorExit";
}

public sealed class PyTypeErrorObjectType : PyExceptionType<PyTypeErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "TypeError";
}

public sealed class PyStopIterationObjectType : PyExceptionType<PyStopIterationObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "StopIteration";
}

public sealed class PyAttributeErrorObjectType : PyExceptionType<PyAttributeErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "AttributeError";
}

public sealed class PyKeyErrorObjectType : PyExceptionType<PyKeyErrorObjectType, PyLookupErrorObjectType>
{
    public override string Module => "builtins";
    public override string Name => "KeyError";
}

public sealed class PyIndexErrorObjectType : PyExceptionType<PyIndexErrorObjectType, PyLookupErrorObjectType>
{
    public override string Module => "builtins";
    public override string Name => "IndexError";
}

public sealed class PyValueErrorObjectType : PyExceptionType<PyValueErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "ValueError";
}

public sealed class PyUnicodeErrorObjectType : PyExceptionType<PyUnicodeErrorObjectType, PyValueErrorObjectType>
{
    public override string Module => "builtins";
    public override string Name => "UnicodeError";
}

public sealed class PyUnicodeEncodeErrorObjectType : PyExceptionType<PyUnicodeEncodeErrorObjectType, PyUnicodeErrorObjectType>
{
    public override string Module => "builtins";
    public override string Name => "UnicodeEncodeError";
}

public sealed class PyNameErrorObjectType : PyExceptionType<PyNameErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "NameError";
}

public sealed class PyUnboundLocalErrorObjectType : PyExceptionType<PyUnboundLocalErrorObjectType, PyNameErrorObjectType>
{
    public override string Module => "builtins";
    public override string Name => "UnboundLocalError";
}

public sealed class PyImportErrorObjectType : PyExceptionType<PyImportErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "ImportError";
}

public sealed class PyModuleNotFoundErrorObjectType : PyExceptionType<PyModuleNotFoundErrorObjectType, PyImportErrorObjectType>
{
    public override string Module => "builtins";
    public override string Name => "ModuleNotFoundError";
}

public sealed class PySyntaxErrorObjectType : PyExceptionType<PySyntaxErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "SyntaxError";
}

public sealed class PyIndentationErrorObjectType : PyExceptionType<PyIndentationErrorObjectType, PySyntaxErrorObjectType>
{
    public override string Module => "builtins";
    public override string Name => "IndentationError";
}

public sealed class PyZeroDivisionErrorObjectType : PyExceptionType<PyZeroDivisionErrorObjectType, PyArithmeticErrorObjectType>
{
    public override string Module => "builtins";
    public override string Name => "ZeroDivisionError";
}

public sealed class PyAssertionErrorObjectType : PyExceptionType<PyAssertionErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "AssertionError";
}

public sealed class PyRuntimeErrorObjectType : PyExceptionType<PyRuntimeErrorObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "RuntimeError";
}

#endregion Concrete Exceptions

#region Warnings

public sealed class PyWarningObjectType : PyExceptionType<PyWarningObjectType, PyExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "Warning";
}

public sealed class PyUserWarningObjectType : PyExceptionType<PyUserWarningObjectType, PyWarningObjectType>
{
    public override string Module => "builtins";
    public override string Name => "UserWarning";
}

public sealed class PySyntaxWarningObjectType : PyExceptionType<PySyntaxWarningObjectType, PyWarningObjectType>
{
    public override string Module => "builtins";
    public override string Name => "SyntaxWarning";
}

public sealed class PyDeprecationWarningObjectType : PyExceptionType<PyDeprecationWarningObjectType, PyWarningObjectType>
{
    public override string Module => "builtins";
    public override string Name => "DeprecationWarning";
}

#endregion
