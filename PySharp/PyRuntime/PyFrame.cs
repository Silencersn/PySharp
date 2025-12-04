using PySharp.AstNodes;
using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime;

public sealed class PyFrame
{
    internal PyFrame()
    {
        Back = null;
        Globals = [];
        Locals = Globals!;
        Exceptions = [];
        GlobalNames = [];
    }
    private PyFrame(PyFrame back, Dictionary<string, PyObject> globals, Dictionary<string, PyObject?> locals)
    {
        Back = back;
        Globals = globals;
        Locals = locals;
        Exceptions = [];
        GlobalNames = [];
    }

    public PyFrame? Back { get; }
    [MemberNotNullWhen(false, nameof(Back))]
    public bool IsRoot => Back is null;
    public Dictionary<string, PyObject> Globals { get; }
    public Dictionary<string, PyObject?> Locals { get; }
    internal HashSet<string> GlobalNames { get; }
    public Stack<PyExceptionObject> Exceptions { get; }
    public PyExceptionObject CurrentException => Exceptions.Peek();

    internal Dictionary<string, PyVariableType>? _variables = null;
    internal Dictionary<string, PyFrame>? _capturedFrames = null;

    internal PyFrame CreateFrame()
    {
        return new PyFrame(this, Globals, []);
    }

    internal PyFrame TempFrame()
    {
        var tempFrame = new PyFrame(this, Globals, Locals.ToDictionary())
        {
            _variables = _variables
        };
        return tempFrame;
    }

    internal void InitArgs(PyArgsDef def, PyArguments arguments)
    {
        for (int i = 0; i < def.PosonlyArgs.Length; i++)
        {
            SetValue(def.PosonlyArgs[i], arguments.Args[i]);
        }
        for (int i = 0; i < def.Args.Length; i++)
        {
            var index = i + def.PosonlyArgs.Length;
            SetValue(def.Args[i], arguments.Args[index]);
        }
        foreach (var kwarg in arguments.Kwargs)
        {
            SetValue(kwarg.Key, kwarg.Value);
        }

        if (def.VarArg is not null)
            SetValue(def.VarArg, PyTupleObject.CreateTuple(arguments.ExtraArgs));
        if (def.KwArg is not null)
            SetValue(def.KwArg, PyDictObject.CreateDict(arguments.ExtraKwargs.Select(static kvp => KeyValuePair.Create((PyObject)PyStrObject.FromString(kvp.Key), kvp.Value))));
    }

    private bool TryGetValueFromBuiltins(string identifier, [NotNullWhen(true)] out PyObject? value)
    {
        value = null;
        if (!Globals.TryGetValue(PySpecialNames.Builtins, out var builtins))
            return false;

        if (!builtins.PyAttributes.TryGetValue(identifier, out value))
            return false;

        return true;
    }

    internal PyObject GetValue(string identifier)
    {
        if (_variables is not null)
        {
            return GetVariableValue(identifier, _variables[identifier]);
        }
        else
        {
            return GetVariableValue(identifier, PyVariableType.Global);
        }
    }

    internal void SetValue(string identifier, PyObject value)
    {
        if (_variables is not null)
        {
            SetVariableValue(identifier, _variables[identifier], value);
        }
        else
        {
            SetVariableValue(identifier, PyVariableType.Global, value);
        }
        return;
    }

    internal PyObject GetVariableValue(string name, PyVariableType variableType)
    {
        if (variableType is PyVariableType.Local or PyVariableType.Parameter)
        {
            if (Locals.TryGetValue(name, out var value))
            {
                if (value is null)
                {
                    PyVirtualMachine.RaiseException(PyStandardExceptionTypes.UnboundLocalError, $"cannot access local variable '{name}' where it is not associated with a value");
                    throw new PyRuntimeException(PyVirtualMachine.CurrentException);
                }

                return value;
            }

            PyVirtualMachine.RaiseException(PyStandardExceptionTypes.NameError, $"name '{name}' is not defined");
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }
        else if (variableType is PyVariableType.Global)
        {
            if (Globals.TryGetValue(name, out var value))
                return value;

            if (TryGetValueFromBuiltins(name, out value))
                return value;

            PyVirtualMachine.RaiseException(PyStandardExceptionTypes.NameError, $"name '{name}' is not defined");
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }
        else if (variableType is PyVariableType.Closure)
        {
            Debug.Assert(_capturedFrames is not null);
            return _capturedFrames[name].GetVariableValue(name, PyVariableType.Local);
        }

        throw new NotImplementedException();
    }
    internal void SetVariableValue(string name, PyVariableType variableType, PyObject value)
    {
        if (variableType is PyVariableType.Local or PyVariableType.Parameter)
        {
            Locals[name] = value;

        }
        else if (variableType is PyVariableType.Global)
        {
            Globals[name] = value;
        }
        else if (variableType is PyVariableType.Closure)
        {
            Debug.Assert(_capturedFrames is not null);
            _capturedFrames[name].SetVariableValue(name, PyVariableType.Local, value);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public void FromModuleImportAll(PyModuleObject module)
    {
        foreach (var attr in module.PyAttributes)
        {
            SetValue(attr.Key, attr.Value);
        }
    }

    public void Import(string name, string? alias = null)
    {
        var module = PyVirtualMachine.PyEnvironment.ImportModule(name);

        if (module is null)
        {
            PyVirtualMachine.RaiseException(PyStandardExceptionTypes.ModuleNotFoundError, $"No module named '{name}'");
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        SetValue(alias ?? name, module);
    }

    public void RemoveValue(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        Locals.Remove(identifier);
    }
}
