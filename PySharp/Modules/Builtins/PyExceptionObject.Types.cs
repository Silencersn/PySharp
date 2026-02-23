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

[PyType("BaseException")]
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

[PyType("Exception")]
public sealed partial class PyExceptionObjectType : PyExceptionType<PyExceptionObjectType, PyBaseExceptionObjectType>;

[PyType("LookupError")]
public sealed partial class PyLookupErrorObjectType : PyExceptionType<PyLookupErrorObjectType, PyExceptionObjectType>;

[PyType("ArithmeticError")]
public sealed partial class PyArithmeticErrorObjectType : PyExceptionType<PyArithmeticErrorObjectType, PyExceptionObjectType>;

#endregion Base Classes

#region Concrete Exceptions

[PyType("SystemExit")]
public sealed partial class PySystemExitObjectType : PyExceptionType<PySystemExitObjectType, PyBaseExceptionObjectType>;

[PyType("GeneratorExit")]
public sealed partial class PyGeneratorExitObjectType : PyExceptionType<PyGeneratorExitObjectType, PyBaseExceptionObjectType>;

[PyType("TypeError")]
public sealed partial class PyTypeErrorObjectType : PyExceptionType<PyTypeErrorObjectType, PyExceptionObjectType>;

[PyType("StopIteration")]
public sealed partial class PyStopIterationObjectType : PyExceptionType<PyStopIterationObjectType, PyExceptionObjectType>;

[PyType("AttributeError")]
public sealed partial class PyAttributeErrorObjectType : PyExceptionType<PyAttributeErrorObjectType, PyExceptionObjectType>;

[PyType("KeyError")]
public sealed partial class PyKeyErrorObjectType : PyExceptionType<PyKeyErrorObjectType, PyLookupErrorObjectType>;

[PyType("IndexError")]
public sealed partial class PyIndexErrorObjectType : PyExceptionType<PyIndexErrorObjectType, PyLookupErrorObjectType>;

[PyType("ValueError")]
public sealed partial class PyValueErrorObjectType : PyExceptionType<PyValueErrorObjectType, PyExceptionObjectType>;

[PyType("UnicodeError")]
public sealed partial class PyUnicodeErrorObjectType : PyExceptionType<PyUnicodeErrorObjectType, PyValueErrorObjectType>;

[PyType("UnicodeEncodeError")]
public sealed partial class PyUnicodeEncodeErrorObjectType : PyExceptionType<PyUnicodeEncodeErrorObjectType, PyUnicodeErrorObjectType>;

[PyType("NameError")]
public sealed partial class PyNameErrorObjectType : PyExceptionType<PyNameErrorObjectType, PyExceptionObjectType>;

[PyType("UnboundLocalError")]
public sealed partial class PyUnboundLocalErrorObjectType : PyExceptionType<PyUnboundLocalErrorObjectType, PyNameErrorObjectType>;

[PyType("ImportError")]
public sealed partial class PyImportErrorObjectType : PyExceptionType<PyImportErrorObjectType, PyExceptionObjectType>;

[PyType("ModuleNotFoundError")]
public sealed partial class PyModuleNotFoundErrorObjectType : PyExceptionType<PyModuleNotFoundErrorObjectType, PyImportErrorObjectType>;

[PyType("SyntaxError")]
public sealed partial class PySyntaxErrorObjectType : PyExceptionType<PySyntaxErrorObjectType, PyExceptionObjectType>;

[PyType("IndentationError")]
public sealed partial class PyIndentationErrorObjectType : PyExceptionType<PyIndentationErrorObjectType, PySyntaxErrorObjectType>;

[PyType("ZeroDivisionError")]
public sealed partial class PyZeroDivisionErrorObjectType : PyExceptionType<PyZeroDivisionErrorObjectType, PyArithmeticErrorObjectType>;

[PyType("AssertionError")]
public sealed partial class PyAssertionErrorObjectType : PyExceptionType<PyAssertionErrorObjectType, PyExceptionObjectType>;

[PyType("RuntimeError")]
public sealed partial class PyRuntimeErrorObjectType : PyExceptionType<PyRuntimeErrorObjectType, PyExceptionObjectType>;

#endregion Concrete Exceptions

#region Warnings

[PyType("Warning")]
public sealed partial class PyWarningObjectType : PyExceptionType<PyWarningObjectType, PyExceptionObjectType>;

[PyType("UserWarning")]
public sealed partial class PyUserWarningObjectType : PyExceptionType<PyUserWarningObjectType, PyWarningObjectType>;

[PyType("SyntaxWarning")]
public sealed partial class PySyntaxWarningObjectType : PyExceptionType<PySyntaxWarningObjectType, PyWarningObjectType>;

[PyType("DeprecationWarning")]
public sealed partial class PyDeprecationWarningObjectType : PyExceptionType<PyDeprecationWarningObjectType, PyWarningObjectType>;

#endregion
