using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyModules.Builtins;

public sealed class PyBaseExceptionGroupObjectType : PyExceptionType<PyBaseExceptionGroupObjectType, PyBaseExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "BaseExceptionGroup";

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!TryParseExceptionGroupInfo(this, args, kwargs, out var info, out var err))
            return err.Value;

        PyTypeObject type = this;
        if (info.Exceptions.All(static exc => PyExceptionObjectType.Shared.IsInstance(exc)))
            type = PyExceptionGroupObjectType.Shared;

        return new PyExceptionObject(type, args) { AsGroup = info };
    }

    internal static bool TryParseExceptionGroupInfo(PyTypeObject exceptionGroupType, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs,
        [NotNullWhen(true)] out ExceptionGroupInfo? info, [NotNullWhen(false)] out PyResult? err)
    {
        info = null;

        if (!PyArgsValidator.ValidateArgs(args, 2, out err))
            return false;

        if (!PyArgsValidator.ValidateEmptyKwargs(kwargs, out err))
            return false;

        if (args[0] is not PyStrObject msg)
        {
            err = PyResult.RaiseTypeError($"{exceptionGroupType.Name}.{PySpecialNames.New}() argument 1 must be str, not {args[0].PyType.FullName}");
            return false;
        }

        IReadOnlyList<PyObject>? excs = args[1] switch
        {
            PyListObject list => list._list,
            PyTupleObject tuple => tuple._array,
            _ => null
        };

        if (excs is null)
        {
            err = PyResult.RaiseTypeError("second argument (exceptions) must be a sequence");
            return false;
        }

        for (var i = 0; i < excs.Count; i++)
        {
            if (excs[i] is not PyExceptionObject)
            {
                err = PyResult.RaiseValueError($"Item {i + 1} of second argument (exceptions) is not an exception");
                return false;
            }
        }

        info = new ExceptionGroupInfo(msg.Value, [.. excs.Cast<PyExceptionObject>()]);
        return true;
    }
}

public sealed class PyExceptionGroupObjectType : PyExceptionType<PyExceptionGroupObjectType>
{
    public override IReadOnlyList<PyTypeObject> Bases => [PyBaseExceptionGroupObjectType.Shared, PyExceptionObjectType.Shared];
    public override string Module => "builtins";
    public override string Name => "ExceptionGroup";

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyBaseExceptionGroupObjectType.TryParseExceptionGroupInfo(this, args, kwargs, out var info, out var err))
            return err.Value;

        if (!info.Exceptions.All(static exc => PyExceptionObjectType.Shared.IsInstance(exc)))
            return PyResult.RaiseTypeError("Cannot nest BaseExceptions in an ExceptionGroup");

        return new PyExceptionObject(this, args) { AsGroup = info };
    }
}