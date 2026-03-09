using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Runtime.InteropServices.Marshalling;

namespace PySharp.Modules.Builtins;

public sealed class PyFunctionObject : PyObject, IPyObjectName
{
    internal readonly PyArgsDef _def;
    internal readonly PyCellObject[]? _closure;
    internal PyObject? _pyClosure;
    internal PyFrame.PyFrameGlobals _globals;
    private readonly PyCodeObject _code;

    public string Name { get; }
    internal ReadOnlySpan<PyCellObject> Closure => _closure;
    internal PyCodeObject Code => _code;

    public override PyTypeObject DefaultPyType => PyFunctionObjectType.Shared;

    internal PyFunctionObject(string name, PyCellObject[]? closure, PyFrame.PyFrameGlobals globals, PyCodeObject code, PyArgsDef def)
    {
        Name = name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        _closure = closure;
        _globals = globals;
        _code = code;
        _def = def;
    }
}

[PyType("function")]
public sealed partial class PyFunctionObjectType : PyTypeObject<PyFunctionObjectType, PyFunctionObject>
{
    protected override PyResult Repr(PyCallContext context, PyFunctionObject self)
    {
        return PyStrObject.FromString($"<function {self.Name} at 0x{self.PyId:X16}>");
    }

    protected override PyResult Call(PyCallContext context, PyFunctionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!self._def.TryParse(args, kwargs, out var arguments))
            return PyResult.TypeError(null /* TODO */);

        var backFrame = context.CurrentFrame;
        var frame = backFrame.CreateFuncCallFrame(self.Name, self, FrameType.Function, (args, kwargs), self._globals, self.Code);

        Debug.Assert(frame.Variables._locals is not null);
        frame.Variables._locals.InitCells(self.Closure);
        frame.InitArgs(self._def, self.Code, arguments);

        using var withFrame = context.WithFrame(frame);
        return PyCore.Eval(context, self.Code.Bytecode);
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
        return self._globals.PyDict;
    }

    [PyProperty(PySpecialNames.Code)]
    private static PyResult Get_Code(PyCallContext context, PyFunctionObject self)
    {
        return self.Code;
    }
}