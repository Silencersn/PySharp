using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Warnings;

public sealed class PyCatchWarningsObject : PyObject
{
    private readonly WarningAction? _action;
    private readonly PyTypeObject<PyExceptionObject> _category;
    private readonly int _lineno;
    private readonly bool _append;
    private WarningStateSnapshot? _snapshot;

    internal PyCatchWarningsObject(
        WarningAction? action,
        PyTypeObject<PyExceptionObject> category,
        int lineno,
        bool append)
    {
        _action = action;
        _category = category;
        _lineno = lineno;
        _append = append;
    }

    public override PyTypeObject DefaultPyType => PyCatchWarningsObjectType.Shared;

    internal PyResult Enter(PyCallContext context)
    {
        if (_snapshot is not null)
            return PyResult.RuntimeError("Cannot enter catch_warnings twice");

        var state = context.PyEnvironment.Warnings;
        _snapshot = state.Capture();
        if (_action is not null)
            state.AddFilter(new WarningFilter(_action.Value, _category, null, null, _lineno), _append);

        return PyNoneObject.None;
    }

    internal PyResult Exit(PyCallContext context)
    {
        if (_snapshot is null)
            return PyResult.RuntimeError("Cannot exit catch_warnings without entering first");

        context.PyEnvironment.Warnings.Restore(_snapshot);
        _snapshot = null;
        return PyNoneObject.None;
    }
}

[PyType("warnings.catch_warnings")]
public sealed partial class PyCatchWarningsObjectType : PyTypeObject<PyCatchWarningsObject>
{
    protected override PyResult Enter(PyCallContext context, PyCatchWarningsObject self)
        => self.Enter(context);

    protected override PyResult Exit(
        PyCallContext context,
        PyCatchWarningsObject self,
        PyObject excType,
        PyObject excVal,
        PyObject excTb)
        => self.Exit(context);
}