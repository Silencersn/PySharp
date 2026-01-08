using PySharp.PyRuntime.Calls;
using System.Diagnostics;

namespace PySharp.PyModules.Builtins;

internal sealed class PyMethodWrapperObject : PyObject
{
    internal readonly PyObject _target;
    internal readonly Delegate _func;

    public override PyTypeObject DefaultPyType => PyMethodWrapperObjectType.Shared;

    internal PyMethodWrapperObject(PyObject target, Delegate func)
    {
        _target = target;
        _func = func;
    }
}

internal sealed class PyMethodWrapperObjectType : PyTypeObject<PyMethodWrapperObjectType, PyMethodWrapperObject>
{
    public override string Name => "method_wrapper";

    protected internal override PyResult Call(PyCallContext context, PyMethodWrapperObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateEmptyKwargs(kwargs, out var err))
            return err.Value;

        return self._func switch
        {
            PyUnaryFunction f => PyArgsValidator.ValidateArgs(args, 0, out err) ? f(context, self._target) : err.Value,
            PyBinaryFunction f => PyArgsValidator.ValidateArgs(args, 1, out err) ? f(context, self._target, args[0]) : err.Value,
            PyTernaryFunction f => PyArgsValidator.ValidateArgs(args, 2, out err) ? f(context, self._target, args[0], args[1]) : err.Value,
            PySelfArgsKwargsFunction f => f(context, self._target, args, kwargs),
            _ => throw new UnreachableException()
        };
    }
}
