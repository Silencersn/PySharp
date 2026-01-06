using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime;

partial class PyFrame
{
    private bool TryLoadFromLocal(string name, [MaybeNullWhen(true)] out PyObject? value)
    {
        return Locals.TryGetValue(name, out value);
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

    internal PyResult LoadFast(int index)
    {
        var locals = _locals.LocalsPlus;
        if (index < 0 || index >= locals.Length)
            return PyResult.RaisePySharpException("out of range");

        var value = locals[index];
        if (value is null)
            return PyResult.RaiseUnboundLocalError($"cannot access local variable '[{index /* TODO: name */}]' where it is not associated with a value");
        return value;
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

    internal PyResult StoreFast(int index, PyObject value)
    {
        var locals = _locals.LocalsPlus;
        if (index < 0 || index >= locals.Length)
            return PyResult.RaisePySharpException("out of range");

        locals[index] = value;
        return PyNoneObject.None;
    }

    public PyResult StoreLocal(string name, PyObject value)
    {
        Locals[name] = value;
        return PyNoneObject.None;
    }

    public PyResult StoreClosure(string name, PyObject value)
    {
        if (_closure is null || !Closures.TryGetValue(name, out var cell))
            return PyResult.RaisePySharpException("closure not found");

        cell.Value = value;
        return PyNoneObject.None;
    }

    public PyResult StoreGlobal(string name, PyObject value)
    {
        Globals[name] = value;
        return PyNoneObject.None;
    }

    public PyResult StoreName(string name, PyObject value)
    {
        return StoreLocal(name, value);
    }

    internal PyResult DeleteFast(int index)
    {
        var locals = _locals.LocalsPlus;
        if (index < 0 || index >= locals.Length)
            return PyResult.RaisePySharpException("out of range");

        if (locals[index] is null)
            return PyResult.RaiseUnboundLocalError($"cannot access local variable '[{index /* TODO: name */}]' where it is not associated with a value");

        locals[index] = null;
        return PyNoneObject.None;
    }

    public PyResult DeleteLocal(string name)
    {
        if (Locals.Remove(name))
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
