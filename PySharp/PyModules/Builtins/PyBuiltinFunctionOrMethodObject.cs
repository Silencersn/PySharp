using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyModules.Builtins;

public class PyBuiltinFunctionOrMethodObject : PyObject, IPyObjectName
{
    public string Name { get; }
    [MemberNotNullWhen(true, nameof(SelfType))]
    public bool IsMethod { get; }
    public PyUncompoundedDelegate PyDelegate { get; }
    public PyObject? Self { get; }
    public PyTypeObject? SelfType { get; }

    public override PyTypeObject DefaultPyType => PyBuiltinFunctionOrMethodObjectType.Shared;

    private PyBuiltinFunctionOrMethodObject(string name, PyUncompoundedDelegate uncompoundedDelegate)
    {
        Name = name;
        IsMethod = false;
        PyDelegate = uncompoundedDelegate;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }
    private PyBuiltinFunctionOrMethodObject(string name, PyObject self, PyTypeObject type, PyUncompoundedDelegate uncompoundedDelegate)
    {
        Self = self;
        SelfType = type;
        Name = name;
        IsMethod = true;
        PyDelegate = uncompoundedDelegate;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        PyAttributes.Add(PySpecialNames.Self, Self);
    }

    internal static PyBuiltinFunctionOrMethodObject CreateFunction(string name, params PyFunction[] funcs)
    {
        return CreateFunction(name, PyDelegateConverter.CreateOverloadDispatcher(funcs));
    }
    internal static PyBuiltinFunctionOrMethodObject CreateFunction(string name, PyUncompoundedDelegate uncompoundedDelegate)
    {
        return new PyBuiltinFunctionOrMethodObject(name, uncompoundedDelegate);
    }
    internal static PyBuiltinFunctionOrMethodObject CreateBoundMethodFromBound(string name, PyObject self, PyTypeObject type, params PyFunction[] funcs)
    {
        return CreateBoundMethodFromBound(name, self, type, PyDelegateConverter.CreateOverloadDispatcher(funcs));
    }
    internal static PyBuiltinFunctionOrMethodObject CreateBoundMethodFromUnbound(string name, PyObject self, PyTypeObject type, PyUncompoundedDelegate uncompoundedDelegate)
    {
        return CreateBoundMethodFromBound(name, self, type, (context, args, kwargs) => uncompoundedDelegate(context, [self, .. args], kwargs));
    }
    internal static PyBuiltinFunctionOrMethodObject CreateBoundMethodFromBound(string name, PyObject self, PyTypeObject type, PyUncompoundedDelegate uncompoundedDelegate)
    {
        return new PyBuiltinFunctionOrMethodObject(name, self, type, uncompoundedDelegate);
    }
}

public sealed class PyBuiltinFunctionOrMethodObjectType : PyTypeObject<PyBuiltinFunctionOrMethodObjectType, PyBuiltinFunctionOrMethodObject>
{
    public override string Module => "builtins";
    public override string Name => "builtin_function_or_method";

    protected override PyResult Repr(PyCallContext context, PyBuiltinFunctionOrMethodObject self)
    {
        if (self.IsMethod)
        {
            if (self.Self is not null)
                return PyStrObject.FromString($"<built-in method {self.Name} of {self.SelfType.Name} object at 0x{self.Self.PyId:X16}>");

            return PyStrObject.FromString($"<method '{self.Name}' of '{self.SelfType.Name}' objects>");
        }

        return PyStrObject.FromString($"<built-in function {self.Name}>");
    }

    protected override PyResult Call(PyCallContext context, PyBuiltinFunctionOrMethodObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self.PyDelegate.Invoke(context, args, kwargs);
    }
}
