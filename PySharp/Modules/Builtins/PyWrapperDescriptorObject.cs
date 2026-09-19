using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

internal sealed class PyWrapperDescriptorObject : PyObject
{
    internal readonly Delegate _func;

    public override PyTypeObject DefaultPyType => PyWrapperDescriptorObjectType.Shared;

    internal PyWrapperDescriptorObject(Delegate func)
    {
        Debug.Assert(func is PyUnaryFunction or PyBinaryFunction or
            PyTernaryFunction or PyQuaternaryFunction or PySelfArgsKwargsFunction);

        _func = func;
    }
}

[PyType("wrapper_descriptor")]
internal sealed partial class PyWrapperDescriptorObjectType : PyTypeObject<PyWrapperDescriptorObject>
{

    protected override PyResult Get(PyCallContext context, PyWrapperDescriptorObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;

        return new PyMethodWrapperObject(instance, self._func);
    }

    protected override PyResult Call(PyCallContext context, PyWrapperDescriptorObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (self._func is not PySelfArgsKwargsFunction && !PyArgsValidator.ValidateEmptyKwargs(kwargs, out var err))
            return err.Value;

        return self._func switch
        {
            PyUnaryFunction f => PyArgsValidator.ValidateArgs(args, 1, out err) ? f(context, args[0]) : err.Value,
            PyBinaryFunction f => PyArgsValidator.ValidateArgs(args, 2, out err) ? f(context, args[0], args[1]) : err.Value,
            // wrap_ternaryfunc / wrap_ternaryfunc_r: the modulus is optional
            // and defaults to None (2 ** 3 and int.__pow__(2, 3) share the
            // wrapper)
            PyTernaryFunction f => args.Count switch
            {
                2 => f(context, args[0], args[1], PyNoneObject.None),
                3 => f(context, args[0], args[1], args[2]),
                _ => args.Count < 2
                    ? PyResult.TypeError(PySR.Runtime_Arguments_MissingArg)
                    : PyResult.TypeError(PySR.Runtime_Arguments_OverflowArgs, 3, args.Count),
            },
            PyQuaternaryFunction f => PyArgsValidator.ValidateArgs(args, 4, out err) ? f(context, args[0], args[1], args[2], args[3]) : err.Value,
            PySelfArgsKwargsFunction f => args.Count > 0 ? f(context, args[0], [.. args.Skip(1)], kwargs) : PyResult.TypeError(null /* TODO */),
            _ => throw new UnreachableException()
        };
    }
}
