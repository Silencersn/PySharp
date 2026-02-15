using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime;

partial class PyFrame
{
    internal sealed class PyFrameVariables
    {
        internal readonly PyFrameGlobals _globals;
        internal readonly PyFrameLocals? _locals;

        public IDictionary<string, PyObject?> Locals => _locals?.Locals ?? _globals.Globals!;
        public IDictionary<string, PyObject> Globals => _globals.Globals;

        private PyFrameVariables(PyFrameGlobals globals, PyFrameLocals? locals)
        {
            _globals = globals;
            _locals = locals;
        }

        public static PyFrameVariables CreateModule()
        {
            return new PyFrameVariables(new PyFrameGlobals(), null);
        }
        public static PyFrameVariables Create(PyFrameGlobals globals, FrozenDictionary<string, int>? localsTable, int cellCount)
        {
            return new PyFrameVariables(globals, new PyFrameLocals(localsTable ?? FrozenDictionary<string, int>.Empty, cellCount));
        }
        public static PyFrameVariables Create(PyFrameGlobals globals, PyFrameLocals? locals)
        {
            return new PyFrameVariables(globals, locals);
        }
        public PyFrameVariables Clone()
        {
            return new PyFrameVariables(_globals.Clone(), _locals?.Clone());
        }

        private bool TryLoadFromLocal(string name, [MaybeNullWhen(true)] out PyObject? value)
        {
            return Locals.TryGetValue(name, out value);
        }

        private bool TryLoadFromBuiltins(string name, [NotNullWhen(true)] out PyObject? value)
        {
            value = null;
            if (!Globals.TryGetValue(PySpecialNames.Builtins, out var builtins))
                return false;

            return builtins.PyAttributes.TryGetValue(name, out value);
        }

        internal IEnumerable<KeyValuePair<string, PyObject>> EnumerateLocals()
        {
            if (_locals is null)
                return Globals;

            var skipCount = _locals.LocalsPlus.Length - _locals._cellCount;

            return _locals._localsTable
                .Where(pair =>
                {
                    var value = _locals.LocalsPlus[pair.Value];
                    if (pair.Value >= skipCount)
                        value = ((PyCellObject)value!).Value;
                    return value is not null;
                })
                .Select(pair =>
                {
                    var value = _locals.LocalsPlus[pair.Value];
                    if (pair.Value >= skipCount)
                        value = ((PyCellObject)value!).Value;
                    return KeyValuePair.Create(pair.Key, value!);
                });
        }

        internal PyResult LoadFast(int index)
        {
            if (_locals is null)
                return PyResult.RaisePySharpException("no locals");

            var locals = _locals.LocalsPlus;
            if (index < 0 || index >= locals.Length)
                return PyResult.RaisePySharpException("out of range");

            var value = locals[index];
            if (value is null)
                return PyResult.UnboundLocalError($"cannot access local variable '[{index /* TODO: name */}]' where it is not associated with a value");
            return value;
        }

        internal PyResult LoadDerefFast(int index)
        {
            var result = LoadFast(index);
            if (result.IsError)
                return result;

            if (result.Value is not PyCellObject cell)
                return PyResult.RaisePySharpException($"variable [{index}] is not cell");

            if (cell.Value is null)
                return PyResult.UnboundLocalError($"cannot access local or free variable '[{index /* TODO: name */}]' where it is not associated with a value");

            return cell.Value;
        }

        public PyResult LoadLocal(string name)
        {
            if (!TryLoadFromLocal(name, out var value))
                return PyResult.NameError($"name '{name}' is not defined");

            if (value is null)
                return PyResult.UnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");

            return value;
        }

        public PyResult LoadDeref(string name)
        {
            var result = LoadLocal(name);
            if (result.IsError)
                return result;

            if (result.Value is not PyCellObject cell)
                return PyResult.RaisePySharpException($"variable '{name}' is not cell");

            if (cell.Value is null)
                return PyResult.UnboundLocalError($"cannot access local or free variable '{name}' where it is not associated with a value");

            return cell.Value;
        }

        public PyResult LoadGlobal(string name)
        {
            if (Globals.TryGetValue(name, out var value))
                return value;

            if (TryLoadFromBuiltins(name, out value))
                return value;

            return PyResult.NameError($"name '{name}' is not defined");
        }

        public PyResult LoadName(string name)
        {
            if (!TryLoadFromLocal(name, out var value))
                return LoadGlobal(name);

            if (value is null)
                return PyResult.UnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");

            return value;
        }

        internal PyResult StoreFast(int index, PyObject value)
        {
            if (_locals is null)
                return PyResult.RaisePySharpException("no locals");

            var locals = _locals.LocalsPlus;
            if (index < 0 || index >= locals.Length)
                return PyResult.RaisePySharpException("out of range");

            locals[index] = value;
            return PyNoneObject.None;
        }

        internal PyResult StoreDerefFast(int index, PyObject? value)
        {
            var result = LoadFast(index);
            if (result.IsError)
                return result;

            if (result.Value is not PyCellObject cell)
                return PyResult.RaisePySharpException($"variable [{index}] is not cell");

            cell.Value = value;
            return PyNoneObject.None;
        }

        public PyResult StoreLocal(string name, PyObject value)
        {
            Locals[name] = value;
            return PyNoneObject.None;
        }

        public PyResult StoreDeref(string name, PyObject? value)
        {
            var result = LoadLocal(name);
            if (result.IsError)
                return result;

            if (result.Value is not PyCellObject cell)
                return PyResult.RaisePySharpException($"variable '{name}' is not cell");

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
            if (_locals is null)
                return PyResult.RaisePySharpException("no locals");

            var locals = _locals.LocalsPlus;
            if (index < 0 || index >= locals.Length)
                return PyResult.RaisePySharpException("out of range");

            if (locals[index] is null)
                return PyResult.UnboundLocalError($"cannot access local variable '[{index /* TODO: name */}]' where it is not associated with a value");

            locals[index] = null;
            return PyNoneObject.None;
        }

        internal PyResult DeleteDerefFast(int index)
        {
            return StoreDerefFast(index, value: null);
        }

        public PyResult DeleteLocal(string name)
        {
            if (Locals.Remove(name))
                return PyNoneObject.None;

            return PyResult.UnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");
        }

        public PyResult DeleteDeref(string name)
        {
            return StoreDeref(name, value: null);
        }

        public PyResult DeleteGlobal(string name)
        {
            if (Globals.Remove(name, out _))
                return PyNoneObject.None;

            return PyResult.NameError($"name '{name}' is not defined");
        }

        public PyResult DeleteName(string name)
        {
            return DeleteLocal(name);
        }
    }
}
