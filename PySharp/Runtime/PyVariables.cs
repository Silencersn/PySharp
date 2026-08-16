using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Buffers;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime;

internal sealed partial class PyVariables
{
    private bool _canDispose;
    private Memory<PyObject?> _memory;
    private PyObject?[]? _localsPlus;
    private readonly FrozenDictionary<string, int>? _localsTable;

    private PyDictObject _globals;
    private IDictionary<string, PyObject?>? _locals;

    private PyVariables(PyDictObject globals)
    {
        _globals = globals;
        _localsTable = null;
    }
    private PyVariables(PyDictObject globals, PyCallContext context, PyCodeObject codeObject)
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
    }
    private PyVariables(PyDictObject globals, FrozenDictionary<string, int> localsTable)
    {
        _globals = globals;
        _localsTable = localsTable;
        _localsPlus = ArrayPool<PyObject?>.Shared.Rent(localsTable.Count);
        _memory = _localsPlus;
        _canDispose = true;
    }
    private PyVariables(PyDictObject globals, IDictionary<string, PyObject?>? locals)
    {
        _localsTable = locals is null ? null : FrozenDictionary<string, int>.Empty;

        _globals = globals;
        _locals = locals;
    }

    [MemberNotNullWhen(true, nameof(_localsTable))]
    internal bool HasLocals => _localsTable is not null;
    internal Memory<PyObject?> LocalsPlusMemory => _memory;
    internal Span<PyObject?> LocalsSpan => LocalsPlusMemory.Span[.._localsTable!.Count];
    internal Span<PyObject?> LocalsSpanUnsafe => LocalsPlusMemory.Span;
    internal Span<PyObject> OperandStackSpan => LocalsPlusMemory.Span[_localsTable!.Count..]!;

    internal PyDictObject Globals => _globals;
    internal FrozenDictionary<string, int> LocalsTable => _localsTable ?? throw new InvalidOperationException();
    internal IDictionary<string, PyObject?> Locals => _locals ?? throw new NotSupportedException();

    internal void MergeThenReplaceGlobals(PyDictObject globals)
    {
        foreach (var pair in _globals)
            globals[pair.Key] = pair.Value;
        _globals = globals;
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
        _memory = default;
        _localsPlus = null!;
        _canDispose = false;
    }

    internal static PyVariables CreateGlobal()
    {
        return new PyVariables([]);
    }
    internal static PyVariables CreateExecEval(PyDictObject globals, IDictionary<string, PyObject?>? locals)
    {
        return new PyVariables(globals, locals);
    }
    internal static PyVariables CreateUsingStackMemoryAllocator(PyDictObject globals, PyCallContext context, PyCodeObject codeObject)
    {
        return new PyVariables(globals, context, codeObject);
    }
    internal static PyVariables CreateUsingArrayPool(PyDictObject globals, FrozenDictionary<string, int> localsTable)
    {
        return new PyVariables(globals, localsTable);
    }
    internal PyVariables CreateForBuildingClass(PyCodeObject codeObject, PyTupleObject? closure)
    {
        if (!HasLocals)
        {
            return new PyVariables(_globals,
                new LocalDictionary(FrozenDictionary<string, int>.Empty, Memory<PyObject?>.Empty));
        }

        // Use the constructor that rents localsPlus and sets _localsTable
        // (free vars). _localsTable is used by TryLoadFromLocals → LoadDeref.
        var vars = new PyVariables(_globals, codeObject.LocalsTable);
        var localsPlus = vars.LocalsSpanUnsafe;

        for (int i = 0; i < codeObject.FreeVars.Length; i++)
        {
            var name = codeObject.FreeVars[i];
            if (_localsTable.TryGetValue(name, out var index))
                localsPlus[i] = LocalsSpan[index];
            // else: leave localsPlus[i] as default (null)
        }

        //   when closure is provided (generic class with type params),
        //   overwrite the tail slots with cell-wrapped type-param values.
        //   Offset = FreeVars.Length - closureTuple.Count relies on the ordering
        //   contract: outer captured variables are added to TempFrees before
        //   type params (see SemanticAnalyzer.FillTempFrees), so FreeVars =
        //   [outer_vars..., type_params...].
        if (closure is not null)
        {
            Debug.Assert(closure.Count > 0);
            Debug.Assert(closure.Count <= codeObject.FreeVars.Length,
                "closure larger than FreeVars — type-param/FreeVar ordering mismatch");

            // overwrite tail slots with type-param cells from closure.
            // Cells are already created in the generic param scope (MakeCell +
            // StoreDeref), so closure tuple items ARE cells — no wrapping needed.
            int offset = codeObject.FreeVars.Length - closure.Count;
            for (int i = 0; i < closure.Count; i++)
                localsPlus[offset + i] = closure[i];
        }

        // Use a plain dict for _locals so that only StoreName'd entries appear
        // in the class namespace — free vars in _localsTable are NOT exposed.
        vars._locals = new Dictionary<string, PyObject?>();
        return vars;
    }
    internal PyVariables CreatePlaceholder()
    {
        return new PyVariables(_globals);
    }
    internal PyVariables CreateInline()
    {
        if (!HasLocals)
            return new PyVariables([.. _globals]);

        var variables = new PyVariables([.. _globals], _localsTable);
        LocalsSpan.CopyTo(variables.LocalsSpan);
        if (_locals is not null)
            variables._locals = new LocalDictionary(_localsTable, variables.LocalsPlusMemory, new Dictionary<string, PyObject?>(_locals));
        return variables;
    }

    private bool TryLoadFromLocals(string name, [MaybeNullWhen(true)] out PyObject? value)
    {
        Debug.Assert(HasLocals, "no locals");

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
        if (!Globals.TryGetValue(PySpecialNames.Interned.Builtins, out var builtins))
            return false;

        return builtins.PyAttributes.TryGetValue(name, out value);
    }

    internal IEnumerable<KeyValuePair<string, PyObject>> EnumerateLocals()
    {
        if (!HasLocals)
            return new StringKeyDict(Globals);

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

    public PyResult LoadLocal(string name)
    {
        Debug.Assert(HasLocals, "no locals");

        if (!TryLoadFromLocals(name, out var value))
            return PyResult.NameError(PySR.Runtime_Variable_NameNotDefined, name);

        if (value is null)
            return PyResult.UnboundLocalError(PySR.Runtime_Variable_UnboundLocalError, name);

        return value;
    }

    public PyResult LoadDeref(string name)
    {
        Debug.Assert(HasLocals, "no locals");

        var result = LoadLocal(name);
        if (result.IsError)
            return result;

        Debug.Assert(result.Value is PyCellObject, $"variable '{name}' is not cell");

        var cell = (PyCellObject)result.Value;

        if (cell.Value is null)
            return PyResult.NameError(PySR.Runtime_Variable_UnboundLocalOrFreeError, name);

        return cell.Value;
    }

    public PyResult LoadGlobal(string name)
    {
        if (Globals.TryGetValue(PyStrObject.FromString(name), out var value))
            return value;

        if (TryLoadFromBuiltins(name, out value))
            return value;

        return PyResult.NameError(PySR.Runtime_Variable_NameNotDefined, name);
    }

    public PyResult LoadName(string name)
    {
        if (!HasLocals)
            return LoadGlobal(name);

        if (!TryLoadFromLocals(name, out var value))
            return LoadGlobal(name);

        if (value is null)
            return PyResult.UnboundLocalError(PySR.Runtime_Variable_UnboundLocalError, name);

        return value;
    }

    public PyResult StoreLocal(string name, PyObject value)
    {
        Debug.Assert(HasLocals, "no locals");

        if (_localsTable.TryGetValue(name, out var index))
            LocalsSpanUnsafe[index] = value;
        else
            Locals[name] = value;

        return PyNoneObject.None;
    }

    public PyResult StoreDeref(string name, PyObject? value)
    {
        Debug.Assert(HasLocals, "no locals");

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
        Globals[PyStrObject.FromString(name)] = value;
        return PyNoneObject.None;
    }

    public PyResult StoreName(string name, PyObject value)
    {
        if (HasLocals)
            return StoreLocal(name, value);

        return StoreGlobal(name, value);
    }

    public PyResult DeleteLocal(string name)
    {
        Debug.Assert(HasLocals, "no locals");

        if (_localsTable.TryGetValue(name, out var index))
        {
            LocalsSpanUnsafe[index] = null;
            return PyNoneObject.None;
        }

        if (_locals is not null && _locals.Remove(name))
            return PyNoneObject.None;

        return PyResult.UnboundLocalError(PySR.Runtime_Variable_UnboundLocalError, name);
    }

    public PyResult DeleteDeref(string name)
    {
        Debug.Assert(HasLocals, "no locals");

        return StoreDeref(name, value: null);
    }

    public PyResult DeleteGlobal(string name)
    {
        if (Globals.Remove(PyStrObject.FromString(name)))
            return PyNoneObject.None;

        return PyResult.NameError(PySR.Runtime_Variable_NameNotDefined, name);
    }

    public PyResult DeleteName(string name)
    {
        if (HasLocals)
            return DeleteLocal(name);

        return DeleteGlobal(name);
    }
}
