using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

internal sealed class PyMethodWrapperObject : PyObject
{
    internal readonly PyObject _target;
    internal readonly Delegate _func;

    public override PyTypeObject DefaultPyType => PyMethodWrapperObjectType.Shared;

    internal PyMethodWrapperObject(PyObject target, Delegate func)
    {
        Debug.Assert(func is PyUnaryFunction or PyBinaryFunction or
            PyTernaryFunction or PyQuaternaryFunction or PySelfArgsKwargsFunction);

        _target = target;
        _func = func;
    }
}

[PyType("method_wrapper")]
internal sealed partial class PyMethodWrapperObjectType : PyTypeObject<PyMethodWrapperObject>
{

    protected override PyResult Call(PyCallContext context, PyMethodWrapperObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateEmptyKwargs(kwargs, out var err))
            return err.Value;

        return self._func switch
        {
            PyUnaryFunction f => PyArgsValidator.ValidateArgs(args, 0, out err) ? f(context, self._target) : err.Value,
            PyBinaryFunction f => PyArgsValidator.ValidateArgs(args, 1, out err) ? f(context, self._target, args[0]) : err.Value,
            // wrap_ternaryfunc / wrap_ternaryfunc_r: the bound form takes
            // the modulus optionally ((2).__pow__(3) and (2).__pow__(3, 5)
            // share the wrapper)
            PyTernaryFunction f => args.Count switch
            {
                1 => f(context, self._target, args[0], PyNoneObject.None),
                2 => f(context, self._target, args[0], args[1]),
                _ => args.Count < 1
                    ? PyResult.TypeError(PySR.Runtime_Arguments_MissingArg)
                    : PyResult.TypeError(PySR.Runtime_Arguments_OverflowArgs, 2, args.Count),
            },
            PyQuaternaryFunction f => PyArgsValidator.ValidateArgs(args, 3, out err) ? f(context, self._target, args[0], args[1], args[2]) : err.Value,
            PySelfArgsKwargsFunction f => f(context, self._target, args, kwargs),
            _ => throw new UnreachableException()
        };
    }
}
