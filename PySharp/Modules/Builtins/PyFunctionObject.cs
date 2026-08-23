using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyFunctionObject : PyObjectManagedDict, IPyObjectName
{
    internal readonly PyArgsDef _def;
    internal readonly PyCellObject[]? _closure;
    internal PyObject? _pyClosure;
    internal PyDictObject _globals;
    private readonly PyCodeObject _code;
    internal PyStrObject? _pyName;


    public string Name { get; internal set; }
    internal ReadOnlySpan<PyCellObject> Closure => _closure;
    internal PyCodeObject Code => _code;
    internal PyStrObject PyName => _pyName ??= PyStrObject.FromString(Name);

    public override PyTypeObject DefaultPyType => PyFunctionObjectType.Shared;

    internal PyFunctionObject(PyCellObject[]? closure, PyDictObject globals, PyCodeObject code, PyArgsDef def)
    {
        Name = code.Name;
        _closure = closure;
        _globals = globals;
        _code = code;
        _def = def;
    }

    internal PyResult InternalCall(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        using var buffer = _def.CreateBuffer();
        if (!_def.TryParse(args, kwargs, buffer, out var arguments))
            return PyResult.TypeError(null /* TODO */);

        ref var backFrame = ref context.CurrentInternalFrame;
        var frame = PyInternalFrame.CreateFuncCallFrame(context, this, FrameType.Function, _globals, _code);

        frame.InitArgs(_def, _code, arguments, Closure);

        using var withFrame = context.WithFrame(ref frame, dispose: false);
        return PyCore.Eval(context, usingLocalsPlusAsOperandStack: _code.Flags is CodeObjectFlags.Function);
    }
}

[PyType("function", IsSealed = true)]
public sealed partial class PyFunctionObjectType : PyTypeObject<PyFunctionObject>
{
    internal override bool IsTypeImmutable => false;

    protected override PyResult Repr(PyCallContext context, PyFunctionObject self)
    {
        return PyStrObject.FromString($"<function {self.Name} at 0x{self.PyId:X16}>");
    }

    protected override PyResult Call(PyCallContext context, PyFunctionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self.InternalCall(context, args, kwargs);
    }

    protected override PyResult Get(PyCallContext context, PyFunctionObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;
        return new PyMethodObject(self, instance);
    }

    [PyProperty(PySpecialNames.Closure)]
    private static PyResult Get_Closure(PyCallContext context, PyFunctionObject self)
    {
        if (self._closure is null)
            return PyNoneObject.None;

        return self._pyClosure ??= PyTupleObject.CreateProxy(self._closure);
    }

    [PyProperty(PySpecialNames.Globals)]
    private static PyResult Get_Globals(PyCallContext context, PyFunctionObject self)
    {
        return self._globals;
    }

    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyFunctionObject self)
    {
        return self.PyName;
    }

    [PyProperty(PySpecialNames.Name, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Name(PyCallContext context, PyFunctionObject self, PyObject value)
    {
        if (value is not PyStrObject { Value: var name })
            return PyResult.TypeError("__name__ must be set to a string object");

        self.Name = name;
        self._pyName = null;
        return PyNoneObject.None;
    }
    

    [PyProperty(PySpecialNames.Code)]
    private static PyResult Get_Code(PyCallContext context, PyFunctionObject self)
    {
        return self.Code;
    }

    [PyProperty(PySpecialNames.Defaults)]
    private static PyResult Get_Defaults(PyCallContext context, PyFunctionObject self)
    {
        if (self._def.Defaults.Length is 0)
            return PyNoneObject.None;
        return PyTupleObject.CreateTuple(self._def.Defaults);
    }
}
