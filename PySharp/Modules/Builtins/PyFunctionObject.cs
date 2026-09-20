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
    internal PyStrObject? _pyQualName;
    internal PyObject? _pyModule;
    internal PyTupleObject? _pyDefaults;


    public string Name { get; internal set; }
    internal ReadOnlySpan<PyCellObject> Closure => _closure;
    internal PyCodeObject Code => _code;
    internal PyStrObject PyName => _pyName ??= PyStrObject.FromString(Name);

    // CPython func_qualname: what the argument binding errors name, mutable
    // through __qualname__ and defaulting to the code object's own name
    internal string QualName => _pyQualName?.Value ?? _code.QualName;

    public override PyTypeObject DefaultPyType => PyFunctionObjectType.Shared;

    internal PyFunctionObject(PyCellObject[]? closure, PyDictObject globals, PyCodeObject code, PyArgsDef def)
    {
        Name = code.Name;
        _closure = closure;
        _globals = globals;
        _code = code;
        _def = def;
        // CPython func_new: __module__ snapshots globals['__name__'] at
        // creation, or stays unset
        if (globals.TryGetValue(PySpecialNames.Name, out var moduleName))
            _pyModule = moduleName;
    }

    internal PyResult InternalCall(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        using var buffer = _def.CreateBuffer();
        if (!_def.TryParse(args, kwargs, buffer, out var arguments))
            return PyResult.TypeError(_def.Describe(args, kwargs).Format(QualName));

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
    internal override bool InstancesAreImmutable => false;

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

    // CPython func_get_qualname/func_set_qualname: created from the code
    // object's co_qualname, writable to str only, deletion rejected
    [PyProperty(PySpecialNames.QualName)]
    private static PyResult Get_QualName(PyCallContext context, PyFunctionObject self)
    {
        return self._pyQualName ??= PyStrObject.FromString(self.Code.QualName);
    }

    [PyProperty(PySpecialNames.QualName, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_QualName(PyCallContext context, PyFunctionObject self, PyObject value)
    {
        if (value is not PyStrObject str)
            return PyResult.TypeError("__qualname__ must be set to a string object");

        self._pyQualName = str;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.QualName, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_QualName(PyCallContext context, PyFunctionObject self)
    {
        return PyResult.TypeError("__qualname__ must be set to a string object");
    }

    // CPython __module__ is a plain T_OBJECT member: snapshotted from
    // globals at creation, writable to any value; a deleted member reads
    // back as None
    [PyProperty(PySpecialNames.Module)]
    private static PyResult Get_Module(PyCallContext context, PyFunctionObject self)
    {
        return self._pyModule ?? PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Module, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Module(PyCallContext context, PyFunctionObject self, PyObject value)
    {
        self._pyModule = value;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Module, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Module(PyCallContext context, PyFunctionObject self)
    {
        self._pyModule = null;
        return PyNoneObject.None;
    }

    // CPython func_get_defaults: the tuple the function holds, None when it
    // holds none; a function whose defaults were never assigned builds its
    // tuple once and hands out that same object afterwards
    [PyProperty(PySpecialNames.Defaults)]
    private static PyResult Get_Defaults(PyCallContext context, PyFunctionObject self)
    {
        if (self._pyDefaults is not null)
            return self._pyDefaults;

        if (self._def.Defaults.Length is 0)
            return PyNoneObject.None;

        return self._pyDefaults = PyTupleObject.CreateTuple(self._def.Defaults);
    }

    // CPython func_set_defaults: a tuple replaces the defaults wholesale, so
    // the parameters that lack a value move with it; a tuple subclass is
    // accepted (PyTuple_Check) and None clears them
    [PyProperty(PySpecialNames.Defaults, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Defaults(PyCallContext context, PyFunctionObject self, PyObject value)
    {
        switch (value)
        {
            case PyNoneObject:
                self._def.Defaults = [];
                self._pyDefaults = null;
                return PyNoneObject.None;

            case PyTupleObject tuple:
                self._def.Defaults = tuple.InternalArray;
                self._pyDefaults = tuple;
                return PyNoneObject.None;

            default:
                return PyResult.TypeError("__defaults__ must be set to a tuple object");
        }
    }

    // del f.__defaults__ (func_set_defaults with NULL) leaves the function
    // holding none, which reads back as None
    [PyProperty(PySpecialNames.Defaults, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Defaults(PyCallContext context, PyFunctionObject self)
    {
        self._def.Defaults = [];
        self._pyDefaults = null;
        return PyNoneObject.None;
    }
}
