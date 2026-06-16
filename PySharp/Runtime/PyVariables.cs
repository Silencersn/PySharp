using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime;

internal sealed partial class PyVariables
{
    private readonly bool _hasLocals;
    private bool _canDispose;
    private Memory<PyObject?> _memory;
    private PyObject?[]? _localsPlus;
    private readonly FrozenDictionary<string, int> _localsTable;

    private readonly PyGlobals _globals;
    private IDictionary<string, PyObject?>? _locals;

    private PyVariables(PyGlobals globals)
    {
        _globals = globals;
        _localsTable = FrozenDictionary<string, int>.Empty;
        _hasLocals = false;
    }
    private PyVariables(PyGlobals globals, PyCallContext context, PyCodeObject codeObject)
    {
        // must be common function (non generator)
        Debug.Assert(codeObject.Flags is CodeObjectFlags.Function);

        _globals = globals;

        _localsTable = codeObject.LocalsTable;

        var size = _localsTable.Count + codeObject.Bytecode.StackSize;
        if (size > 0 && size < PyCallContextFrameState.PyObjectMemoryAllocator.DataChunkSize)
            _memory = context.FrameState.Alloc(size);
        else
            _memory = _localsPlus = ArrayPool<PyObject?>.Shared.Rent(size);

        _canDispose = true;
        _hasLocals = true;
    }
    private PyVariables(PyGlobals globals, FrozenDictionary<string, int> localsTable)
    {
        _globals = globals;
        _localsTable = localsTable;
        _localsPlus = ArrayPool<PyObject?>.Shared.Rent(localsTable.Count);
        _memory = _localsPlus;
        _canDispose = true;
        _hasLocals = true;
    }
    private PyVariables(PyGlobals globals, IDictionary<string, PyObject?>? locals)
    {
        _localsTable = FrozenDictionary<string, int>.Empty;

        _globals = globals;
        _locals = locals;
        _hasLocals = _locals is not null;
    }

    internal Memory<PyObject?> LocalsPlusMemroy => _memory;
    internal Span<PyObject?> LocalsSpan => LocalsPlusMemroy.Span[.._localsTable.Count];
    internal Span<PyObject?> LocalsSpanUnsafe => LocalsPlusMemroy.Span;
    internal Span<PyObject> OperandStackSpan => LocalsPlusMemroy.Span[_localsTable.Count..]!;

    internal PyGlobals Globals => _globals;
    internal IDictionary<string, PyObject> GlobalsDict => Globals.Dict;
    internal FrozenDictionary<string, int> LocalsTable => _localsTable;
    internal IDictionary<string, PyObject?> Locals
    {
        get
        {
            if (_locals is not null)
                return _locals;

            throw new NotSupportedException();
        }
    }

    // do not call Dispose if the frame is exposed to the outside
    public void Dispose(PyCallContext context)
    {
        if (!_canDispose)
            return;

        if (_localsPlus is null)
        {
            context.FrameState.Free(_memory);
            _memory = default;
            _canDispose = false;
            return;
        }

        ArrayPool<PyObject?>.Shared.Return(_localsPlus, clearArray: true);
        _localsPlus = null!;
        _canDispose = false;
    }

    internal IEnumerable<KeyValuePair<string, PyObject>> EnumerateVariablesForBuildingClass()
    {
        Debug.Assert(_locals is LocalDictionary);
        var extra = ((LocalDictionary)_locals).ExtraLocals;
        if (extra is null)
            return [];

        return extra.Where(static pair => pair.Value is not null)!;
    }

    internal static PyVariables CreateGlobal()
    {
        var dict = new Dictionary<string, PyObject>(StringComparer.Ordinal);
        return new PyVariables(new PyGlobals(dict));
    }
    internal static PyVariables CreateExecEval(PyGlobals globals, IDictionary<string, PyObject?>? locals)
    {
        return new PyVariables(globals, locals);
    }
    internal static PyVariables CreateUsingStackMemoryAllocator(PyGlobals globals, PyCallContext context, PyCodeObject codeObject)
    {
        return new PyVariables(globals, context, codeObject);
    }
    internal static PyVariables CreateUsingArrayPool(PyGlobals globals, FrozenDictionary<string, int> localsTable)
    {
        return new PyVariables(globals, localsTable);
    }
    internal PyVariables CreateForBuildingClass(PyCodeObject codeObject)
    {
        if (!_hasLocals)
            return new PyVariables(_globals,
                new LocalDictionary(FrozenDictionary<string, int>.Empty, Memory<PyObject?>.Empty));

        var localsPlus = new PyObject?[codeObject.LocalsTable.Count];
        Debug.Assert(localsPlus is not null);

        var span = LocalsSpan;
        for (int i = 0; i < codeObject.FreeVars.Length; i++)
        {
            var name = codeObject.FreeVars[i];
            var obj = span[_localsTable[name]];
            localsPlus[i] = obj;
        }

        var locals = new LocalDictionary(codeObject.LocalsTable, localsPlus);
        return new PyVariables(_globals, locals);
    }
    internal PyVariables CreatePlaceholder()
    {
        return new PyVariables(_globals);
    }
    internal PyVariables CreateInline()
    {
        if (!_hasLocals)
            return new PyVariables(_globals.Clone());

        var variables = new PyVariables(_globals.Clone(), _localsTable);
        LocalsSpan.CopyTo(variables.LocalsSpan);
        if (_locals is not null)
            variables._locals = new LocalDictionary(_localsTable, variables.LocalsPlusMemroy, new Dictionary<string, PyObject?>(_locals));
        return variables;
    }

    private bool TryLoadFromLocals(string name, [MaybeNullWhen(true)] out PyObject? value)
    {
        Debug.Assert(_hasLocals, "no locals");

        if (_localsTable.TryGetValue(name, out var index))
        {
            value = LocalsSpanUnsafe[index];
            return true;
        }

        if (_locals is not null)
            return _locals.TryGetValue(name, out value);

        value = null;
        return false;
    }

    private bool TryLoadFromBuiltins(string name, [NotNullWhen(true)] out PyObject? value)
    {
        value = null;
        if (!GlobalsDict.TryGetValue(PySpecialNames.Builtins, out var builtins))
            return false;

        return builtins.PyAttributes.TryGetValue(name, out value);
    }

    internal IEnumerable<KeyValuePair<string, PyObject>> EnumerateLocals()
    {
        if (!_hasLocals)
            return GlobalsDict;

        if (_locals is not null)
        {
            return _locals
                .Select(static pair =>
                {
                    if (pair.Value is not PyCellObject cell)
                        return pair;

                    return KeyValuePair.Create(pair.Key, cell.Value);
                })
                .Where(static pair => pair.Value is not null)!;
        }

        return _localsTable
            .Select(pair =>
            {
                var value = LocalsSpan[pair.Value];
                if (value is PyCellObject cell)
                    value = cell.Value;
                return KeyValuePair.Create(pair.Key, value);
            })
            .Where(static pair => pair.Value is not null)!;
    }

    internal PyResult LoadFast(int index)
    {
        Debug.Assert(_hasLocals, "no locals");

        var locals = LocalsSpanUnsafe;

        Debug.Assert(index >= 0 && index < locals.Length, "index out of range");

        var value = locals[index];
        if (value is null)
            return PyResult.UnboundLocalError($"cannot access local variable '[{index /* TODO: name */}]' where it is not associated with a value");
        return value;
    }

    internal PyResult LoadDerefFast(int index)
    {
        Debug.Assert(_hasLocals, "no locals");

        var result = LoadFast(index);
        if (result.IsError)
            return result;

        Debug.Assert(result.Value is PyCellObject, $"variable [{index}] is not cell");

        var cell = (PyCellObject)result.Value;

        if (cell.Value is null)
            return PyResult.UnboundLocalError($"cannot access local or free variable '[{index /* TODO: name */}]' where it is not associated with a value");

        return cell.Value;
    }

    public PyResult LoadLocal(string name)
    {
        Debug.Assert(_hasLocals, "no locals");

        if (!TryLoadFromLocals(name, out var value))
            return PyResult.NameError($"name '{name}' is not defined");

        if (value is null)
            return PyResult.UnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");

        return value;
    }

    public PyResult LoadDeref(string name)
    {
        Debug.Assert(_hasLocals, "no locals");

        var result = LoadLocal(name);
        if (result.IsError)
            return result;

        Debug.Assert(result.Value is PyCellObject, $"variable '{name}' is not cell");

        var cell = (PyCellObject)result.Value;

        if (cell.Value is null)
            return PyResult.UnboundLocalError($"cannot access local or free variable '{name}' where it is not associated with a value");

        return cell.Value;
    }

    public PyResult LoadGlobal(string name)
    {
        if (GlobalsDict.TryGetValue(name, out var value))
            return value;

        if (TryLoadFromBuiltins(name, out value))
            return value;

        return PyResult.NameError($"name '{name}' is not defined");
    }

    public PyResult LoadName(string name)
    {
        if (!_hasLocals)
            return LoadGlobal(name);

        if (!TryLoadFromLocals(name, out var value))
            return LoadGlobal(name);

        if (value is null)
            return PyResult.UnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");

        return value;
    }

    internal PyResult StoreFast(int index, PyObject value)
    {
        Debug.Assert(_hasLocals, "no locals");

        var locals = LocalsSpanUnsafe;

        Debug.Assert(index >= 0 && index < locals.Length, "index out of range");

        locals[index] = value;
        return PyNoneObject.None;
    }

    internal PyResult StoreDerefFast(int index, PyObject? value)
    {
        Debug.Assert(_hasLocals, "no locals");

        var result = LoadFast(index);
        if (result.IsError)
            return result;

        Debug.Assert(result.Value is PyCellObject, $"variable [{index}] is not cell");

        var cell = (PyCellObject)result.Value;

        cell.Value = value;
        return PyNoneObject.None;
    }

    public PyResult StoreLocal(string name, PyObject value)
    {
        Debug.Assert(_hasLocals, "no locals");

        if (_localsTable.TryGetValue(name, out var index))
            LocalsSpanUnsafe[index] = value;
        else
            Locals[name] = value;

        return PyNoneObject.None;
    }

    public PyResult StoreDeref(string name, PyObject? value)
    {
        Debug.Assert(_hasLocals, "no locals");

        var result = LoadLocal(name);
        if (result.IsError)
            return result;

        Debug.Assert(result.Value is PyCellObject, $"variable '{name}' is not cell");

        var cell = (PyCellObject)result.Value;

        cell.Value = value;
        return PyNoneObject.None;
    }

    public PyResult StoreGlobal(string name, PyObject value)
    {
        GlobalsDict[name] = value;
        return PyNoneObject.None;
    }

    public PyResult StoreName(string name, PyObject value)
    {
        if (_hasLocals)
            return StoreLocal(name, value);

        return StoreGlobal(name, value);
    }

    internal PyResult DeleteFast(int index)
    {
        Debug.Assert(_hasLocals, "no locals");

        var locals = LocalsSpanUnsafe;

        Debug.Assert(index >= 0 && index < locals.Length, "index out of range");

        if (locals[index] is null)
            return PyResult.UnboundLocalError($"cannot access local variable '[{index /* TODO: name */}]' where it is not associated with a value");

        locals[index] = null;
        return PyNoneObject.None;
    }

    internal PyResult DeleteDerefFast(int index)
    {
        Debug.Assert(_hasLocals, "no locals");

        return StoreDerefFast(index, value: null);
    }

    public PyResult DeleteLocal(string name)
    {
        Debug.Assert(_hasLocals, "no locals");

        if (_localsTable.TryGetValue(name, out var index))
        {
            LocalsSpanUnsafe[index] = null;
            return PyNoneObject.None;
        }

        if (_locals is not null && _locals.Remove(name))
            return PyNoneObject.None;

        return PyResult.UnboundLocalError($"cannot access local variable '{name}' where it is not associated with a value");
    }

    public PyResult DeleteDeref(string name)
    {
        Debug.Assert(_hasLocals, "no locals");

        return StoreDeref(name, value: null);
    }

    public PyResult DeleteGlobal(string name)
    {
        if (GlobalsDict.Remove(name))
            return PyNoneObject.None;

        return PyResult.NameError($"name '{name}' is not defined");
    }

    public PyResult DeleteName(string name)
    {
        if (_hasLocals)
            return DeleteLocal(name);

        return DeleteGlobal(name);
    }
}
