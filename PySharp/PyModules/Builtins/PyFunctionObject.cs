using PySharp.AstNodes;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyFunctionObject : PyObject, IPyObjectName
{
    internal readonly PyUncompoundedDelegate _function;
    internal readonly PyCellObject[]? _closure;
    internal PyObject? _pyClosure;
    internal PyFrame.PyFrameGlobals _globals;
    internal readonly PyCodeObject _code;

    public string Name { get; }
    internal ReadOnlySpan<PyCellObject> Closure => _closure;

    public override PyTypeObject DefaultPyType => PyFunctionObjectType.Shared;

    internal PyFunctionObject(string name, PyUncompoundedDelegate function, IEnumerable<PyCellObject>? closure, PyFrame.PyFrameGlobals globals, PyCodeObject code)
    {
        Name = name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        _function = function;
        _closure = closure?.ToArray();
        _globals = globals;
        _code = code;
    }
}

public sealed class PyFunctionObjectType : PyTypeObject<PyFunctionObjectType, PyFunctionObject>
{
    public override string Module => "builtins";
    public override string Name => "function";

    public PyFunctionObjectType()
    {
        AppendMemberDescriptor(PySpecialNames.Closure,
            static (_, func) => func._pyClosure ??= func._closure is not null ? PyTupleObject.CreateProxy(func._closure) : PyNoneObject.None);
        AppendMemberDescriptor(PySpecialNames.Globals,
            static (_, func) => func._globals.PyDict);
        AppendMemberDescriptor(PySpecialNames.Code,
            static (_, func) => func._code);
    }

    protected override PyResult Repr(PyCallContext context, PyFunctionObject self)
    {
        return PyStrObject.FromString($"<function {self.Name} at 0x{self.PyId:X16}>");
    }

    protected override PyResult Call(PyCallContext context, PyFunctionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self._function.Invoke(context, args, kwargs);
    }

    protected override PyResult Get(PyCallContext context, PyFunctionObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;
        return new PyMethodObject(self, instance);
    }
}