using PySharp.PyRuntime.Calls;
using System.Diagnostics;

namespace PySharp.PyModules.Builtins;

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

internal sealed class PyWrapperDescriptorObjectType : PyTypeObject<PyWrapperDescriptorObjectType, PyWrapperDescriptorObject>
{
    public override string Module => "builtins";
    public override string Name => "wrapper_descriptor";

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
            PyTernaryFunction f => PyArgsValidator.ValidateArgs(args, 3, out err) ? f(context, args[0], args[1], args[2]) : err.Value,
            PyQuaternaryFunction f => PyArgsValidator.ValidateArgs(args, 4, out err) ? f(context, args[0], args[1], args[2], args[3]) : err.Value,
            PySelfArgsKwargsFunction f => args.Count > 0 ? f(context, args[0], [.. args.Skip(1)], kwargs) : PyResult.RaiseTypeError("needs an argument"),
            _ => throw new UnreachableException()
        };
    }
}
