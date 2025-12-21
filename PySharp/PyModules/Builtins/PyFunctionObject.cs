using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyFunctionObject : PyObject, IPyObjectName
{
    private readonly PyOldUncompoundedFunction _function;
    internal readonly PyCellObject[]? _closure;
    internal PyObject? _pyClosure;
    internal PyFrame.PyFrameGlobals _globals;

    public string Name { get; }
    internal ReadOnlySpan<PyCellObject> CapturedVariables => _closure;

    public override PyTypeObject DefaultPyType => PyFunctionObjectType.Shared;

    internal PyFunctionObject(string name, PyOldUncompoundedFunction function, IEnumerable<PyCellObject>? closure, PyFrame.PyFrameGlobals globals)
    {
        Name = name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        _function = function;
        _closure = closure?.ToArray();
        _globals = globals;
    }

    protected internal override PyObject? ReprImpl()
    {
        return PyStrObject.FromString($"<function {Name} at 0x{PyId:X16}>");
    }

    protected internal override PyObject? CallImpl(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _function.Invoke(args, kwargs);
    }

    protected internal override PyObject? GetImpl(PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return this;

        return new PyMethodObject(this, instance);
    }
}

public sealed class PyFunctionObjectType : PyPrimitiveTypeObject<PyFunctionObjectType, PyFunctionObject>
{
    public override string Name => "function";

    public PyFunctionObjectType()
    {
        AppendMemberDescriptor<PyFunctionObject>(PySpecialNames.Closure,
            static func => func._pyClosure ??= func._closure is not null ? PyTupleObject.CreateProxy(func._closure) : PyNoneObject.None);
        AppendMemberDescriptor<PyFunctionObject>(PySpecialNames.Globals,
            static func => func._globals.PyDict);
    }
}