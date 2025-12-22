using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System;
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
    public PyUncompoundedDelegate PyDelegate { get; }
    public PyObject? Self { get; }
    public PyTypeObject? SelfType { get; }

    public override PyTypeObject DefaultPyType => PyBuiltinFunctionOrMethodObjectType.Shared;

    internal PyBuiltinFunctionOrMethodObject(string name, params PyFunction[] funcs)
    {
        Name = name;
        IsMethod = false;
        Self = null;
        PyDelegate = (context, args, kwargs) =>
        {
            EnsureDefCache(funcs.Select(static func => func.Method));
            foreach (var func in funcs)
            {
                if (_defCache[func.Method].TryParse(args, kwargs, out var result))
                    return func.Invoke(context, result);
            }

            return PyResult.RaiseTypeError(null);
        };
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }
    internal PyBuiltinFunctionOrMethodObject(string name, PyObject self, PyTypeObject type, params PyFunction[] funcs)
    {
        Self = self;
        SelfType = type;
        Name = name;
        IsMethod = true;
        PyDelegate = (context, args, kwargs) =>
        {
            EnsureDefCache(funcs.Select(static func => func.Method));
            foreach (var func in funcs)
            {
                if (_defCache[func.Method].TryParse(args, kwargs, out var result))
                    return func.Invoke(context, result);
            }

            return PyResult.RaiseTypeError(null);
        };
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        PyAttributes.Add(PySpecialNames.Self, Self);
    }
    internal PyBuiltinFunctionOrMethodObject(string name, PyTypeObject type, params MethodInfo/*PyMethod<TObject>*/[] methods)
    {
        Name = name;
        IsMethod = false;
        PyDelegate = (context, args, kwargs) =>
        {
            if (args.Count is 0 || !type.IsInstance(args[0]))
                return PyResult.RaiseTypeError(null);

            EnsureDefCache(methods);

            var self = args[0];
            args = [.. args.Skip(1)];
            foreach (var method in methods)
            {
                if (_defCache[method].TryParse(args, kwargs, out var result))
                    return (PyResult)method.Invoke(method.IsStatic ? null : type, [context, self, result])!;
            }

            return PyResult.RaiseTypeError(null);
        };
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }
    internal PyBuiltinFunctionOrMethodObject(string name, PyObject self, PyTypeObject type, MethodInfo/*PyMethod<TObject>*/[] methods)
    {
        Self = self;
        SelfType = type;
        Name = name;
        IsMethod = true;
        PyDelegate = (context, args, kwargs) =>
        {
            EnsureDefCache(methods);

            foreach (var method in methods)
            {
                if (_defCache[method].TryParse(args, kwargs, out var result))
                    return (PyResult)method.Invoke(method.IsStatic ? null : type, [context, self, result])!;
            }

            return PyResult.RaiseTypeError(null);
        };
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        PyAttributes.Add(PySpecialNames.Self, Self);
    }

    internal PyBuiltinFunctionOrMethodObject(string name, PyUncompoundedDelegate func)
    {
        Name = name;
        PyDelegate = func;
        IsMethod = false;
        Self = null;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }


    [MemberNotNull(nameof(_defCache))]
    private void EnsureDefCache(IEnumerable<MethodInfo> methodInfos)
    {
        if (_defCache is null)
        {
            lock (this)
            {
                if (_defCache is null)
                {
                    var cache = new Dictionary<MethodInfo, PyArgsDef>();
                    foreach (var methInfo in methodInfos)
                    {
                        var argsDef = methInfo.GetCustomAttribute<PyFunctionArgsDefAttribute>();
                        Debug.Assert(argsDef is not null);
                        var def = PyArgsDef.FromDef(argsDef.Parameters);
                        cache[methInfo] = def;
                    }
                    _defCache = cache;
                }
            }
        }
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
