using PySharp.AstNodes;
using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using System.Collections;
using System.Collections.Frozen;
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
        Closures = [];

        _proxyLocals = _proxyGlobals = new ProxyDict(Locals);
    }
    private PyFrame(PyFrame back, Dictionary<string, PyObject> globals, Dictionary<string, PyObject?> locals)
    {
        Back = back;
        Globals = globals;
        Locals = locals;
        Closures = [];
        Exceptions = [];
        GlobalNames = [];
        _proxyLocals = new ProxyDict(Locals);
        _proxyGlobals = new ProxyDict(Globals!);
    }

    public PyFrame? Back { get; }
    [MemberNotNullWhen(false, nameof(Back))]
    public bool IsRoot => Back is null;
    public Dictionary<string, PyObject> Globals { get; }
    public Dictionary<string, PyObject?> Locals { get; }
    public Dictionary<string, PyCellObject> Closures { get; }
    internal HashSet<string> GlobalNames { get; }
    public Stack<PyExceptionObject> Exceptions { get; }
    public PyExceptionObject CurrentException => Exceptions.Peek();

    internal FrozenDictionary<string, PyVariableType>? _variables = null;
    internal ProxyDict _proxyGlobals;
    internal ProxyDict _proxyLocals;

    internal PyFrame CreateFrame(bool newGlobals = false)
    {
        return new PyFrame(this, newGlobals ? [] : Globals, []);
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

            if (Closures.TryGetValue(name, out var cell))
            {
                if (cell.Value is null)
                {
                    PyVirtualMachine.RaiseException(PyStandardExceptionTypes.UnboundLocalError, $"cannot access local variable '{name}' where it is not associated with a value");
                    throw new PyRuntimeException(PyVirtualMachine.CurrentException);
                }

                return cell.Value;
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
            //Debug.Assert(_capturedFrames is not null);
            //return _capturedFrames[name].GetVariableValue(name, PyVariableType.Local);

            var value = Closures[name].Value;
            if (value is not null)
                return value;

            PyVirtualMachine.RaiseException(PyStandardExceptionTypes.NameError, $"cannot access free variable '{name}' where it is not associated with a value in enclosing scope");
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        throw new NotImplementedException();
    }
    internal void SetVariableValue(string name, PyVariableType variableType, PyObject value)
    {
        if (variableType is PyVariableType.Local or PyVariableType.Parameter)
        {
            if (Closures.TryGetValue(name, out PyCellObject? cell))
                cell.Value = value;
            else
                Locals[name] = value;
        }
        else if (variableType is PyVariableType.Global)
        {
            Globals[name] = value;
        }
        else if (variableType is PyVariableType.Closure)
        {
            //Debug.Assert(_capturedFrames is not null);
            //_capturedFrames[name].SetVariableValue(name, PyVariableType.Local, value);

            Closures[name].Value = value;
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
        if (!PyVirtualMachine.PyEnvironment.TryLoadModule(name, out var module))
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

    internal sealed class ProxyDict : IDictionary<PyObject, PyObject>
    {
        private readonly Dictionary<string, PyObject?> _origDict;
        private readonly Dictionary<PyObject, PyObject> _extraDict;

        public ProxyDict(Dictionary<string, PyObject?> dict)
        {
            _origDict = dict;
            _extraDict = [];
        }

        PyObject IDictionary<PyObject, PyObject>.this[PyObject key]
        {
            get
            {
                if (key is PyStrObject strObj)
                    return _origDict[strObj.Value] ?? throw new KeyNotFoundException(strObj.Value);
                return _extraDict[key];
            }
            set
            {
                if (key is PyStrObject strObj)
                    _origDict[strObj.Value] = value;
                else
                    _extraDict[key] = value;
            }
        }

        ICollection<PyObject> IDictionary<PyObject, PyObject>.Keys => [
                .. _extraDict.Keys,
                .. _origDict.Select(static kvp => PyStrObject.FromString(kvp.Key)),
            ];

        ICollection<PyObject> IDictionary<PyObject, PyObject>.Values => [
                .. _extraDict.Values,
                .. _origDict.Values.Where(static value => value is not null)!,
            ];

        int ICollection<KeyValuePair<PyObject, PyObject>>.Count => _origDict.Count(static kvp => kvp.Value is not null) + _extraDict.Count;

        bool ICollection<KeyValuePair<PyObject, PyObject>>.IsReadOnly => false;

        void IDictionary<PyObject, PyObject>.Add(PyObject key, PyObject value)
        {
            if (key is PyStrObject strObj)
                _origDict.Add(strObj.Value, value);
            else
                _extraDict[key] = value;
        }

        void ICollection<KeyValuePair<PyObject, PyObject>>.Add(KeyValuePair<PyObject, PyObject> item)
        {
            if (item.Key is PyStrObject strObj)
                _origDict.Add(strObj.Value, item.Value);
            else
                _extraDict[item.Key] = item.Value;
        }

        void ICollection<KeyValuePair<PyObject, PyObject>>.Clear()
        {
            _origDict.Clear();
            _extraDict.Clear();
        }

        bool ICollection<KeyValuePair<PyObject, PyObject>>.Contains(KeyValuePair<PyObject, PyObject> item)
        {
            if (item.Key is PyStrObject strObj)
                return _origDict.Contains(KeyValuePair.Create<string, PyObject?>(strObj.Value, item.Value));
            return _extraDict.Contains(item);
        }

        bool IDictionary<PyObject, PyObject>.ContainsKey(PyObject key)
        {
            if (key is PyStrObject strObj)
                return _origDict.ContainsKey(strObj.Value);
            return _extraDict.ContainsKey(key);
        }

        void ICollection<KeyValuePair<PyObject, PyObject>>.CopyTo(KeyValuePair<PyObject, PyObject>[] array, int arrayIndex)
        {
            foreach (var kvp in _origDict)
            {
                if (kvp.Value is null)
                    continue;

                array[arrayIndex++] = KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(kvp.Key), kvp.Value);
            }
            foreach (var kvp in _extraDict)
            {
                array[arrayIndex++] = kvp;
            }
        }

        IEnumerator<KeyValuePair<PyObject, PyObject>> IEnumerable<KeyValuePair<PyObject, PyObject>>.GetEnumerator()
        {
            return _origDict
                .Where(static kvp => kvp.Value is not null)
                .Select(static kvp => KeyValuePair.Create((PyObject)PyStrObject.FromString(kvp.Key), kvp.Value!))
                .Concat(_extraDict)
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<PyObject, PyObject>>)this).GetEnumerator();
        }

        bool IDictionary<PyObject, PyObject>.Remove(PyObject key)
        {
            if (key is PyStrObject strObj)
                return _origDict.Remove(strObj.Value);
            return _extraDict.Remove(key);
        }

        bool ICollection<KeyValuePair<PyObject, PyObject>>.Remove(KeyValuePair<PyObject, PyObject> item)
        {
            if (item.Key is PyStrObject strObj)
                return ((ICollection<KeyValuePair<string, PyObject?>>)_origDict).Remove(KeyValuePair.Create<string, PyObject?>(strObj.Value, item.Value));
            return ((ICollection<KeyValuePair<PyObject, PyObject>>)_extraDict).Remove(item);
        }

        bool IDictionary<PyObject, PyObject>.TryGetValue(PyObject key, [NotNullWhen(true)] out PyObject? value)
        {
            if (key is PyStrObject strObj)
                return _origDict.TryGetValue(strObj.Value, out value) && value is not null;
            return _extraDict.TryGetValue(key, out value);
        }
    }
}
