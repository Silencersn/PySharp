using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyFunctionObject : PyObject, IPyObjectName
{
    internal readonly PyUncompoundedDelegate _function;
    internal readonly PyCellObject[]? _closure;
    internal PyObject? _pyClosure;
    internal PyFrame.PyFrameGlobals _globals;

    public string Name { get; }
    internal ReadOnlySpan<PyCellObject> CapturedVariables => _closure;

    public override PyTypeObject DefaultPyType => PyFunctionObjectType.Shared;

    internal PyFunctionObject(string name, PyUncompoundedDelegate function, IEnumerable<PyCellObject>? closure, PyFrame.PyFrameGlobals globals)
    {
        Name = name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        _function = function;
        _closure = closure?.ToArray();
        _globals = globals;
    }
}

public sealed class PyFunctionObjectType : PyTypeObject<PyFunctionObjectType, PyFunctionObject>
{
    public override string Name => "function";

    public PyFunctionObjectType()
    {
        AppendMemberDescriptor<PyFunctionObject>(PySpecialNames.Closure,
            static func => func._pyClosure ??= func._closure is not null ? PyTupleObject.CreateProxy(func._closure) : PyNoneObject.None);
        AppendMemberDescriptor<PyFunctionObject>(PySpecialNames.Globals,
            static func => func._globals.PyDict);
    }

    protected internal override PyResult Repr(PyCallContext context, PyFunctionObject self)
    {
        return PyStrObject.FromString($"<function {self.Name} at 0x{self.PyId:X16}>");
    }

    protected internal override PyResult Call(PyCallContext context, PyFunctionObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self._function.Invoke(context, args, kwargs);
    }

    protected internal override PyResult Get(PyCallContext context, PyFunctionObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;
        return new PyMethodObject(self, instance);
    }
}