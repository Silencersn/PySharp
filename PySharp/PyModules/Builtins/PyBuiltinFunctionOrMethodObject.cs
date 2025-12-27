using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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

    internal PyBuiltinFunctionOrMethodObject(string name, params PyFunction[] funcs)
    {
        Name = name;
        IsMethod = false;
        Self = null;
        PyDelegate = PyDelegateConverter.CreateOverloadDispatcher(funcs);
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }
    internal PyBuiltinFunctionOrMethodObject(string name, PyObject self, PyTypeObject type, params PyFunction[] funcs)
    {
        Self = self;
        SelfType = type;
        Name = name;
        IsMethod = true;
        PyDelegate = PyDelegateConverter.CreateOverloadDispatcher(funcs);
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        PyAttributes.Add(PySpecialNames.Self, Self);
    }
    internal PyBuiltinFunctionOrMethodObject(string name, PyUncompoundedDelegate uncompoundedDelegate)
    {
        Name = name;
        IsMethod = false;
        PyDelegate = uncompoundedDelegate;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }
    internal PyBuiltinFunctionOrMethodObject(string name, PyObject self, PyTypeObject type, PyUncompoundedDelegate uncompoundedDelegate)
    {
        Self = self;
        SelfType = type;
        Name = name;
        IsMethod = true;
        PyDelegate = (context, args, kwargs) => uncompoundedDelegate(context, [self, .. args], kwargs);
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        PyAttributes.Add(PySpecialNames.Self, Self);
    }
}

public sealed class PyBuiltinFunctionOrMethodObjectType : PyTypeObject<PyBuiltinFunctionOrMethodObjectType, PyBuiltinFunctionOrMethodObject>
{
    public override string Name => "builtin_function_or_method";

    protected internal override PyResult Repr(PyCallContext context, PyBuiltinFunctionOrMethodObject self)
    {
        if (self.IsMethod)
        {
            if (self.Self is not null)
                return PyStrObject.FromString($"<built-in method {self.Name} of {self.SelfType.Name} object at 0x{self.Self.PyId:X16}>");

            return PyStrObject.FromString($"<method '{self.Name}' of '{self.SelfType.Name}' objects>");
        }

        return PyStrObject.FromString($"<built-in function {self.Name}>");
    }

    protected internal override PyResult Call(PyCallContext context, PyBuiltinFunctionOrMethodObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self.PyDelegate.Invoke(context, args, kwargs);
    }
}
