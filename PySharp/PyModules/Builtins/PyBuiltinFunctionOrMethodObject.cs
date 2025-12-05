using PySharp.PyRuntime;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PySharp.PyModules.Builtins;

public class PyBuiltinFunctionOrMethodObject : PyObject, IPyObjectName
{
    private Dictionary<MethodInfo, PyArgsDef>? _defCache;

    public string Name { get; }
    [MemberNotNullWhen(true, nameof(SelfType))]
    public bool IsMethod { get; }
    public PyUncompoundedFunction Function { get; }
    public PyObject? Self { get; }
    public PyTypeObject? SelfType { get; }

    internal PyBuiltinFunctionOrMethodObject(string name, params PyFunction[] funcs)
    {
        Name = name;
        IsMethod = false;
        Self = null;
        Function = (args, kwargs) =>
        {
            if (_defCache is null)
            {
                _defCache = [];
                foreach (var func in funcs)
                {
                    var argsDef = func.Method.GetCustomAttribute<PyFunctionArgsDefAttribute>();
                    Debug.Assert(argsDef is not null);
                    var def = PyArgsDef.FromDef(argsDef.Parameters);
                    _defCache[func.Method] = def;
                }
            }

            foreach (var func in funcs)
            {
                if (_defCache[func.Method].TryParse(args, kwargs, out var result))
                    return func.Invoke(result);
            }

            return PyVirtualMachine.RaiseTypeError(null);
        };
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }
    internal PyBuiltinFunctionOrMethodObject(string name, PyObject self, PyTypeObject type, params PyFunction[] funcs)
    {
        Self = self;
        SelfType = type;
        Name = name;
        IsMethod = true;
        Function = (args, kwargs) =>
        {
            if (_defCache is null)
            {
                _defCache = [];
                foreach (var func in funcs)
                {
                    var argsDef = func.Method.GetCustomAttribute<PyFunctionArgsDefAttribute>();
                    Debug.Assert(argsDef is not null);
                    var def = PyArgsDef.FromDef(argsDef.Parameters);
                    _defCache[func.Method] = def;
                }
            }

            foreach (var func in funcs)
            {
                if (_defCache[func.Method].TryParse(args, kwargs, out var result))
                    return func.Invoke(result);
            }

            return PyVirtualMachine.RaiseTypeError(null);
        };
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        PyAttributes.Add(PySpecialNames.Self, Self);
    }

    internal PyBuiltinFunctionOrMethodObject(string name, PyUncompoundedFunction func)
    {
        Name = name;
        Function = func;
        IsMethod = false;
        Self = null;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }
    internal PyBuiltinFunctionOrMethodObject(string name, Func<PyObject, PyObject?> func) : this(name, (args, kwargs) =>
    {
        if (args.Count is not 1 || kwargs.Count > 0)
            return PyVirtualMachine.RaiseTypeError(null);
        return func.Invoke(args[0]);
    })
    {
    }

    public override PyObject? Repr()
    {
        if (IsMethod)
        {
            if (Self is not null)
                return PyStrObject.FromString($"<built-in method {Name} of {SelfType.Name} object at {Self.PyId:X16}>");

            return PyStrObject.FromString($"<method '{Name}' of '{SelfType.Name}' objects>");
        }

        return PyStrObject.FromString($"<built-in function {Name}>");
    }

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return Function.Invoke(args, kwargs);
    }
}

public sealed class PyBuiltinFunctionOrMethodObjectType : PyTypeObject
{
    public override string Name => "builtin_function_or_method";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }
}
