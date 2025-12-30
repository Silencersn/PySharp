using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.Utility;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime;

partial class PyFrame
{
    private bool TryLoadFromLocal(string name, [MaybeNullWhen(true)] out PyObject? value)
    {
        value = null;
        if (_locals is null)
            return false;

        return _locals.TryGetValue(name, out value);
    }

    private bool TryLoadFromClosure(string name, [MaybeNullWhen(true)] out PyObject? value)
    {
        value = null;
        if (_closure is null)
            return false;

        if (!_closure.TryGetValue(name, out var cell))
            return false;

        value = cell.Value;
        return true;
    }

    private bool TryLoadFromBuiltins(string name, [NotNullWhen(true)] out PyObject? value)
    {
        value = null;
        if (!Globals.TryGetValue(PySpecialNames.Builtins, out var builtins))
            return false;

        return builtins.PyAttributes.TryGetValue(name, out value);
    }

    public PyResult LoadLocal(string name)
    {
        if (!TryLoadFromLocal(name, out var value))
            return PyResult.RaiseNameError($"name '{name}' is not defined");

        if (value is null)
            return PyResult.RaiseUnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");

        return value;
    }

    public PyResult LoadClosure(string name, bool isLocal)
    {
        if (!TryLoadFromClosure(name, out var value))
            return PyResult.RaiseNameError($"name '{name}' is not defined");

        if (value is null)
        {
            if (isLocal)
                return PyResult.RaiseUnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");
            
            return PyResult.RaiseUnboundLocalError($"cannot access free variable '{name}' where it is not associated with a value in enclosing scope");
        }

        return value;
    }

    public PyResult LoadGlobal(string name)
    {
        if (Globals.TryGetValue(name, out var value))
            return value;

        if (TryLoadFromBuiltins(name, out value))
            return value;

        return PyResult.RaiseNameError($"name '{name}' is not defined");
    }

    public PyResult LoadName(string name)
    {
        if (!TryLoadFromLocal(name, out var value))
            return LoadGlobal(name);

        if (value is null)
            return PyResult.RaiseUnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");

        return value;
    }

    public void StorgeLocal(string name, PyObject value)
    {
        Locals[name] = value;
    }

    public void StorgeClosure(string name, PyObject value)
    {
        Closures[name].Value = value;
    }

    public void StorgeGlobal(string name, PyObject value)
    {
        Globals[name] = value;
    }

    public void StorgeName(string name, PyObject value)
    {
        Locals[name] = value;
    }

    public PyResult DeleteLocal(string name)
    {
        if (_locals is not null && _locals.Remove(name))
            return PyNoneObject.None;

        return PyResult.RaiseUnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");
    }

    public PyResult DeleteClosure(string name, bool isLocal)
    {
        if (_closure is not null && _closure.Remove(name))
            return PyNoneObject.None;

        if (isLocal)
            return PyResult.RaiseUnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");

        return PyResult.RaiseUnboundLocalError($"cannot access free variable '{name}' where it is not associated with a value in enclosing scope");
    }

    public PyResult DeleteGlobal(string name)
    {
        if (Globals.TryRemove(name, out _))
            return PyNoneObject.None;

        return PyResult.RaiseNameError($"name '{name}' is not defined");
    }

    public PyResult DeleteName(string name)
    {
        return DeleteLocal(name);
    }
}
