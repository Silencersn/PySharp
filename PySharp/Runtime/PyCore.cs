using PySharp.Compilation.Primitives;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.VirtualMachine;
using System.Collections.Generic;
using System.Diagnostics;

namespace PySharp.Runtime;

internal static class PyCore
{
    public static PyResult Eval(PyCallContext context, bool usingLocalsPlusAsOperandStack = false)
    {
        Debug.Assert(context.CurrentInternalFrame.CodeObject is not null);
        var vmStates = new BytecodeVirtualMachineStates(context, usingLocalsPlusAsOperandStack);
        var savedHandledException = context.HandledException;
        try
        {
            return BytecodeVirtualMachine.Eval(context, ref vmStates);
        }
        finally
        {
            context.HandledException = savedHandledException;
        }
    }

    public static PyCellObject[]? GetFreeVars(ref PyInternalFrame frame, PyCodeObject code)
    {
        if (code.FreeVars.Length is 0)
            return null;

        var cells = new PyCellObject[code.FreeVars.Length];

        var variables = frame.Variables;
        var span = variables.LocalsSpan;
        var table = variables.LocalsTable;
        for (int i = 0; i < code.FreeVars.Length; i++)
        {
            var name = code.FreeVars[i];

            PyObject obj;
            if (table.TryGetValue(name, out var index))
            {
                // function

                Debug.Assert(span[index] is not null);
                obj = span[index]!;
            }
            else
            {
                // class

                Debug.Assert(variables.Locals[name] is not null);
                obj = variables.Locals[name]!;
            }
            Debug.Assert(obj is PyCellObject);
            cells[i] = (PyCellObject)obj;
        }

        return cells;
    }

    public static PyFunctionObject MakeFunction(ref PyInternalFrame frame, PyCodeObject codeObject, PyArgsDef def)
    {
        return new PyFunctionObject(GetFreeVars(ref frame, codeObject), frame.Variables.Globals, codeObject, def);
    }

    public static PyObject BuildClass(PyCallContext context, PyCodeObject codeObject, List<PyTypeObject> bases, OrderedDictionary<string, PyObject> kwargs, PyTupleObject? closure)
    {
        PyTypeObject metaClass = PyTypeObjectType.Shared;

        if (bases.Count > 0)
            metaClass = bases[0].PyType;

        if (kwargs.Remove("metaclass", out var metaObj))
        {
            if (metaObj is not PyTypeObject typeObj)
                throw context.PySharpException("non-type metaclass is not supported");

            metaClass = typeObj;
        }

        // check if there is a conflict and find the most derived metaclass
        for (int i = 0; i < bases.Count; i++)
        {
            var otherMetaClass = bases[i].PyType;

            if (otherMetaClass.IsSubclassOf(metaClass))
                metaClass = otherMetaClass;
            else if (!metaClass.IsSubclassOf(otherMetaClass))
                throw context.TypeError(PySR.Runtime_Inheritance_MetaclassConflict);
        }

        // PEP 3115: the class body runs in the mapping returned by the
        // metaclass __prepare__ hook. CPython resolves the hook with a full
        // attribute lookup on the metaclass (bltinmodule.c
        // builtin___build_class__) and passes the class kwargs through; the
        // same object then reaches the metaclass call as the namespace.
        // type's own __prepare__ is immutable and returns a fresh dict, so
        // metaclass-free creation skips the lookup.
        PyObject ns;
        IPyVariablesLocalsDict classLocals;
        if (ReferenceEquals(metaClass, PyTypeObjectType.Shared))
        {
            ns = new PyDictObject();
            classLocals = (PyDictObject)ns;
        }
        else
        {
            ns = PrepareClassNamespace(context, metaClass, codeObject.Name, bases, kwargs);
            classLocals = PyVariables.CreateClassLocals(context, ns);
        }

        if (bases.Count is 0)
            bases.Add(PyObjectType.Shared);

        var newFrame = context.CurrentInternalFrame.CreateClassBuildFrame(codeObject, closure, classLocals);
        using (var withFrame = context.WithFrame(ref newFrame))
            Eval(context).PyUnwrap(context);

        var nameStr = PyStrObject.FromString(codeObject.Name);
        var basesTuple = PyTupleObject.CreateTuple(bases);
        var args = PyTupleObject.CreateTuple([nameStr, basesTuple, ns]);

        var newFunc = metaClass.Slots.New ?? throw context.PySharpException("metaclass {0} has no __new__ slot", metaClass.QualName);
        var obj = newFunc(context, metaClass, args, kwargs).PyUnwrap(context);
        if (!metaClass.IsInstance(obj))
            return obj;

        if (metaClass != PyTypeObjectType.Shared)
            _ = PyTypeObjectType.CallInit(context, metaClass, obj, args, kwargs).PyUnwrap(context);

        return obj;
    }

    // builtin___build_class__ (bltinmodule.c): resolve the metaclass
    // __prepare__ hook with a plain attribute lookup (descriptor binding
    // included — a classmethod binds to the metaclass, a plain function
    // stays unbound), call it with (name, bases, **class kwargs) and accept
    // only a mapping back. type.__prepare__ always resolves on a metaclass
    // MRO, so a miss here can only come from a metaclass __getattr__
    // reporting AttributeError — CPython treats that as "no hook" and
    // defaults to a plain dict.
    private static PyObject PrepareClassNamespace(PyCallContext context, PyTypeObject metaClass, string name, List<PyTypeObject> bases, OrderedDictionary<string, PyObject> kwargs)
    {
        var basesTuple = PyTupleObject.CreateTuple(bases);

        var prepResult = PyOperators.GetAttr(context, metaClass, PySpecialNames.Prepare);
        if (prepResult.IsError)
        {
            if (prepResult.IsAttributeError)
                return new PyDictObject();

            throw new PyRuntimeException(context, prepResult.Exception);
        }

        var nsResult = PySpecialMethods.Call(context, prepResult.Value, [PyStrObject.FromString(name), basesTuple], kwargs);
        if (nsResult.IsError)
            throw new PyRuntimeException(context, nsResult.Exception);

        var ns = nsResult.Value;
        if (ns.PyType.Slots.GetItem is null)
            throw context.TypeError(PySR.Runtime_Inheritance_PrepareMustReturnMapping, metaClass.Name, ns.PyType.Name);

        return ns;
    }

    public static PyNoneObject ImportAllFrom(PyCallContext context, ref PyInternalFrame frame, PyModuleObject module)
    {
        // if module has __all__, import only those names
        // item in __all__ must be str
        if (module.PyAttributes.TryGetValue(PySpecialNames.All, out var all))
        {
            // unlike cpython, allows iterable
            var list = PyUtils.IterableToList(context, all).PyUnwrap(context);

            foreach (var item in list)
            {
                if (item is not PyStrObject strObj)
                    throw context.TypeError(PySR.Runtime_Import_NonStringAllElt, module.Name, item.PyType.Name);

                var attr = PyOperators.GetAttr(context, module, strObj.Value).PyUnwrap(context);
                frame.Variables.StoreName(strObj.Value, attr).PyUnwrap(context);
            }
        }
        else
        {
            foreach (var kvp in module.PyAttributes)
            {
                // only import names that do not start with '_'
                if (!kvp.Key.StartsWith('_'))
                    frame.Variables.StoreName(kvp.Key, kvp.Value).PyUnwrap(context);
            }
        }
        return PyNoneObject.None;
    }

    // interactive expression statements echo through this intrinsic
    // (CPython CALL_INTRINSIC_1 / INTRINSIC_PRINT -> print_expr): write
    // repr(value) to stdout unless it is None, and bind builtins._ to the
    // value
    public static PyObject DisplayHook(PyCallContext context, PyObject value)
    {
        var builtins = context.PyEnvironment.LoadBuiltinModule(context, "builtins");

        if (value is not PyNoneObject)
        {
            var reprResult = PySpecialMethods.Repr(context, value);
            if (reprResult.IsError)
                return reprResult.Exception;
            context.Out.WriteLine(reprResult.Value.Value);
        }

        builtins.PyAttributes[PySpecialNames.Underscore] = value;
        return PyNoneObject.None;
    }

    public static void Raise(PyCallContext context, ref BytecodeVirtualMachineStates states, PyObject? excObj, PyObject? causeObj)
    {
        PyExceptionObject exc;
        if (excObj is null)
        {
            // bare raise re-raises the current exception as-is; inside an
            // except* handler the stack top is the matched subgroup (a null
            // entry is a fully-consumed rest, not a re-raisable exception).
            // When this frame has no active exception, fall back to the
            // call chain's handled exception (CPython reads the thread's
            // exc_info, which callees observe dynamically).
            if (states.Exceptions.Count is 0 || states.Exceptions.Peek() is null)
                exc = context.HandledException ?? throw context.RuntimeError(PySR.Runtime_RaiseStmt_NoActiveException);
            else
                exc = states.CurrentException;
        }
        else
        {
            // an explicit raise starts a fresh traceback head (CPython
            // replaces it too); PrepReraiseStar exploits that reference
            // change to tell an explicit re-raise from a bare one
            exc = ToException(context, excObj, isCause: false)!;
            exc.WithTraceback(context, overwriteExisting: true);
        }

        if (causeObj is not null)
        {
            // CPython: an explicit cause (an exception or None) always
            // suppresses the implicit context in the traceback chain
            exc.SuppressContext = true;

            if (causeObj is PyNoneObject)
            {
                exc.Cause = null;
            }
            else
            {
                exc.Cause = ToException(context, causeObj, isCause: true);
                exc.CauseReason = PySR.Runtime_RaiseStmt_Cause;
            }
        }

        // The frame's innermost handled exception; an except* body keeps the
        // matched subgroup on the frame stack while HandledException still
        // names the original group. An empty stack means the raise came from
        // outside this frame's try regions, so fall back to the context-wide
        // slot (CPython reads the thread-state slot, which callees observe).
        var pre = states.Exceptions.TryPeek(out var frameTop) && frameTop is not null
            ? frameTop
            : context.HandledException;
        if (pre is not null && !ReferenceEquals(pre, exc))
        {
            BreakContextLinkTo(pre, exc);
            exc.Context = pre;
        }

        // Chaining is done above with _PyErr_SetObject's overwrite semantics,
        // so the exception must not run through the constructor's
        // set-time chaining again
        throw new PyRuntimeException(exc);

        static PyExceptionObject? ToException(PyCallContext context, PyObject? pyObj, bool isCause)
        {
            if (pyObj is null)
                return null;

            if (pyObj is PyExceptionObject excObj)
            {
                return excObj;
            }
            else if (pyObj is PyTypeObject typeObj && typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
            {
                return PyExceptionObject.Create(context, typeObj, []).PyUnwrap(context);
            }
            else
            {
                var message = isCause
                    ? PySR.Runtime_RaiseStmt_CauseNonException
                    : PySR.Runtime_RaiseStmt_RaiseNonException;
                throw context.TypeError(message);
            }
        }
    }

    // Generator throw-in/close injection settles the injected exception's
    // context at each frame re-injection (CPython _PyErr_ChainStackItem via
    // gen_send_ex exc=1): the generator frame's own handled slot chains on
    // with overwrite semantics and never falls back to the caller's slot;
    // with an empty slot the context is left as-is (gen_close raised the
    // GeneratorExit before swapping the exception state, so it keeps the
    // close caller's chain). ContextSettled keeps non-injection propagation
    // hops from re-chaining later.
    internal static void SettleInjectedContext(BytecodeVirtualMachineStates states, PyExceptionObject exc)
    {
        if (states.Exceptions.TryPeek(out var own) && own is not null && !ReferenceEquals(own, exc))
        {
            BreakContextLinkTo(own, exc);
            exc.Context = own;
        }

        exc.ContextSettled = true;
    }

    // CPython _PyErr_SetObject clears any link pointing at value in the
    // handled exception's context chain before (re)setting value's context,
    // so re-raising an ancestor cannot close a cycle — the traceback printer
    // recurses along Context and a cycle would overflow the native stack.
    // Floyd's slow pointer bounds the walk when the chain already contains a
    // foreign cycle.
    internal static void BreakContextLinkTo(PyExceptionObject handled, PyExceptionObject exc)
    {
        var node = handled;
        var slow = handled;
        var slowToggle = false;
        while (node.Context is { } context)
        {
            if (ReferenceEquals(context, exc))
            {
                node.Context = null;
                return;
            }

            node = context;
            if (ReferenceEquals(node, slow))
                return;

            if (slowToggle && slow.Context is { } slowNext)
                slow = slowNext;
            slowToggle = !slowToggle;
        }
    }

    // _PyEval_CheckExceptTypeValid: the match type must be an exception
    // class or a tuple of them; except* reaches it through
    // CheckExceptStarTypeValid, plain except enforces the same rule in
    // MakeExceptCondition
    internal static void CheckExceptTypeValid(PyCallContext context, PyObject type)
    {
        var valid = type is PyTypeObject typeObj && typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared)
            || type is PyTupleObject tupleObj && tupleObj.All(obj => obj is PyTypeObject t && t.IsSubclassOf(PyBaseExceptionObjectType.Shared));
        if (!valid)
            throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);
    }

    // _PyEval_CheckExceptStarTypeValid: except* refuses a match type that is
    // a BaseExceptionGroup subclass, or a tuple containing one
    internal static void CheckExceptStarTypeValid(PyCallContext context, PyObject type)
    {
        CheckExceptTypeValid(context, type);

        var groupType = type is PyTupleObject tupleObj
            ? tupleObj.Any(IsExceptionGroupType)
            : IsExceptionGroupType(type);
        if (groupType)
            throw context.TypeError(PySR.Runtime_TryStmt_CatchExceptionGroupWithExceptStar);

        static bool IsExceptionGroupType(PyObject obj)
            => obj is PyTypeObject typeObj && typeObj.IsSubclassOf(PyBaseExceptionGroupObjectType.Shared);
    }

    public static Func<PyExceptionObject, bool> MakeExceptCondition(PyCallContext context, PyObject type)
    {
        if (type is PyTypeObject typeObj)
        {
            if (!typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);

            return typeObj.IsInstance;
        }
        else if (type is PyTupleObject tupleObj)
        {
            if (!tupleObj.All(obj => obj is PyTypeObject t && t.IsSubclassOf(PyBaseExceptionObjectType.Shared)))
                throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);

            return exc => tupleObj.Any(obj => ((PyTypeObject)obj).IsInstance(exc));
        }
        else
        {
            throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);
        }
    }

    public static (PyExceptionObject? RestExc, PyObject MatchedExc) SplitExceptionGroup(PyCallContext context, PyExceptionObject exception, PyObject type)
    {
        var splitResult = exception.CallMethod(context, "split", [type]).PyUnwrap(context);
        if (splitResult is not PyTupleObject tuple)
            throw context.TypeError(PySR.Runtime_TryStmt_SplitReturnsNonTuple, exception.PyType.TpName, splitResult.PyType.TpName);

        if (tuple.Count is not 2)
            throw context.TypeError(PySR.Runtime_TryStmt_SplitReturnsTupleWithWrongSize, exception.PyType.TpName, tuple.Count);

        var match = tuple[0];
        var restObj = tuple[1];
        var rest = restObj is PyNoneObject ? null : (restObj as PyExceptionObject) ??
            throw context.TypeError(PySR.Runtime_TryStmt_ExpectedExceptionOrNone, tuple[1].PyType.TpName);

        return (rest, match);
    }

    // Port of _PyExc_PrepReraiseStar (Objects/exceptions.c): settles the
    // exception a try-except* statement leaves behind. orig is the caught
    // exception, excs the per-handler raises (the unmatched rest appended
    // as None). Reraised items (metadata identical to orig - a bare raise
    // never mutates them) are projected back onto orig's own tree keeping
    // its message; everything else becomes a sibling in a fresh group.
    public static PyExceptionObject? PrepReraiseStar(PyCallContext context, PyExceptionObject orig, IReadOnlyList<PyExceptionObject?> excs)
    {
        var items = excs.Where(static e => e is not null).Cast<PyExceptionObject>().ToList();
        if (items.Count is 0)
            return null;

        var prepared = PrepReraiseStarCore(context, orig, items);
        if (prepared is not null)
            // CPython's RERAISE of the settlement result uses
            // PyErr_SetRaisedException, which never chains: mark the context
            // final so propagation hops cannot attach the ambient handler
            prepared.ContextSettled = true;
        return prepared;
    }

    private static PyExceptionObject? PrepReraiseStarCore(PyCallContext context, PyExceptionObject orig, List<PyExceptionObject> items)
    {
        if (!orig.IsGroup)
            // a naked exception was caught and wrapped; only one except*
            // clause could have executed, so at most one item remains
            return items[0];
        List<PyExceptionObject> raised = [];
        List<PyExceptionObject> reraised = [];
        foreach (var item in items)
        {
            if (IsSameExceptionMetadata(item, orig))
                reraised.Add(item);
            else
                raised.Add(item);
        }

        var reraisedGroup = reraised.Count > 0 ? ExceptionGroupProjection(context, orig, reraised) : null;

        if (raised.Count is 0)
            return reraisedGroup;

        if (reraisedGroup is not null)
            raised.Add(reraisedGroup);

        return raised.Count is 1 ? raised[0] : PyBaseExceptionGroupObjectType.CreateExceptionGroup(string.Empty, raised);
    }

    private static bool IsSameExceptionMetadata(PyExceptionObject first, PyExceptionObject second)
    {
        return ReferenceEquals(first.Traceback, second.Traceback)
            && ReferenceEquals(first.Cause, second.Cause)
            && ReferenceEquals(first.Context, second.Context);
    }

    // Port of exception_group_projection: the sub-group of eg whose leaves
    // appear (by identity) in any of keep, rebuilt along eg's own nesting.
    private static PyExceptionObject? ExceptionGroupProjection(PyCallContext context, PyExceptionObject eg, List<PyExceptionObject> keep)
    {
        var leafIds = new HashSet<PyExceptionObject>(ReferenceEqualityComparer.Instance);
        foreach (var item in keep)
            CollectGroupLeaves(item, leafIds);

        return ProjectGroup(context, eg, leafIds);
    }

    private static void CollectGroupLeaves(PyExceptionObject exc, HashSet<PyExceptionObject> leafIds)
    {
        if (exc.IsGroup)
        {
            foreach (var sub in exc.AsGroup.Exceptions)
                CollectGroupLeaves(sub, leafIds);
        }
        else
        {
            leafIds.Add(exc);
        }
    }

    private static PyExceptionObject? ProjectGroup(PyCallContext context, PyExceptionObject eg, HashSet<PyExceptionObject> leafIds)
    {
        if (!eg.IsGroup)
            return leafIds.Contains(eg) ? eg : null;

        List<PyExceptionObject> match = [];
        foreach (var sub in eg.AsGroup.Exceptions)
        {
            var projected = ProjectGroup(context, sub, leafIds);
            if (projected is not null)
                match.Add(projected);
        }

        if (match.Count is 0)
            return null;

        // derive keeps the group's own message and metadata, like the
        // split machinery the handler entry used
        var derived = eg.CallMethod(context, "derive", [PyListObject.CreateList(match)]).PyUnwrap(context);
        if (derived is not PyExceptionObject result || !result.IsGroup || !PyBaseExceptionGroupObjectType.Shared.IsInstance(result))
            throw context.TypeError(PySR.Runtime_ExceptionGroup_DeriveReturnNonGroup);

        return result;
    }

    public static PyResult EvalOperator(PyCallContext context, OperatorType op, PyObject left, PyObject right)
    {
        return op switch
        {
            OperatorType.Add => PyOperators.Add(context, left, right),
            OperatorType.Sub => PyOperators.Sub(context, left, right),
            OperatorType.Mult => PyOperators.Mult(context, left, right),
            OperatorType.MatMult => PyOperators.MatMult(context, left, right),
            OperatorType.Div => PyOperators.TrueDiv(context, left, right),
            OperatorType.Mod => PyOperators.Mod(context, left, right),
            OperatorType.Pow => PyOperators.Pow(context, left, right, PyNoneObject.None),
            OperatorType.LShift => PyOperators.LShift(context, left, right),
            OperatorType.RShift => PyOperators.RShift(context, left, right),
            OperatorType.BitOr => PyOperators.BitOr(context, left, right),
            OperatorType.BitXor => PyOperators.BitXor(context, left, right),
            OperatorType.BitAnd => PyOperators.BitAnd(context, left, right),
            OperatorType.FloorDiv => PyOperators.FloorDiv(context, left, right),
            _ => throw new UnreachableException(),
        };
    }

    public static PyResult EvalOperator(PyCallContext context, UnaryOpType op, PyObject value)
    {
        return op switch
        {
            UnaryOpType.Invert => PyOperators.Invert(context, value),
            UnaryOpType.Not => PyOperators.Not(context, value),
            UnaryOpType.UAdd => PyOperators.UAdd(context, value),
            UnaryOpType.USub => PyOperators.USub(context, value),
            _ => throw new UnreachableException(),
        };
    }

    public static PyResult EvalOperator(PyCallContext context, CmpopType op, PyObject left, PyObject right)
    {
        return op switch
        {
            CmpopType.Eq => PyOperators.Eq(context, left, right),
            CmpopType.NotEq => PyOperators.NotEq(context, left, right),
            CmpopType.Lt => PyOperators.Lt(context, left, right),
            CmpopType.LtE => PyOperators.LtE(context, left, right),
            CmpopType.Gt => PyOperators.Gt(context, left, right),
            CmpopType.GtE => PyOperators.GtE(context, left, right),
            CmpopType.Is => PyOperators.Is(left, right),
            CmpopType.IsNot => PyOperators.IsNot(left, right),
            CmpopType.In => PyOperators.In(context, left, right),
            CmpopType.NotIn => PyOperators.NotIn(context, left, right),
            _ => throw new UnreachableException(),
        };
    }

    public static PyResult EvalInplaceOperator(PyCallContext context, OperatorType op, PyObject left, PyObject right)
    {
        return op switch
        {
            OperatorType.Add => PyOperators.InPlaceAdd(context, left, right),
            OperatorType.Sub => PyOperators.InPlaceSub(context, left, right),
            OperatorType.Mult => PyOperators.InPlaceMult(context, left, right),
            OperatorType.MatMult => PyOperators.InPlaceMatMult(context, left, right),
            OperatorType.Div => PyOperators.InPlaceTrueDiv(context, left, right),
            OperatorType.Mod => PyOperators.InPlaceMod(context, left, right),
            OperatorType.Pow => PyOperators.InPlacePow(context, left, right, PyNoneObject.None),
            OperatorType.LShift => PyOperators.InPlaceLShift(context, left, right),
            OperatorType.RShift => PyOperators.InPlaceRShift(context, left, right),
            OperatorType.BitOr => PyOperators.InPlaceBitOr(context, left, right),
            OperatorType.BitXor => PyOperators.InPlaceBitXor(context, left, right),
            OperatorType.BitAnd => PyOperators.InPlaceBitAnd(context, left, right),
            OperatorType.FloorDiv => PyOperators.InPlaceFloorDiv(context, left, right),
            _ => throw new UnreachableException(),
        };
    }

    internal static bool IsSequenceForMatch(PyObject obj)
    {
        return obj switch
        {
            PyListObject or
            PyTupleObject or
            PyRangeObject => true,

            PyStrObject => false, // str is not regarded as sequence

            _ => false,// TODO: support other valid sequences
        };
    }

    internal static bool IsMappingForMatch(PyObject obj)
    {
        return obj switch
        {
            PyDictObject => true,
            _ => false,// TODO: support other valid mapping
        };
    }

    internal static PyResult GetAttrOrMethod(PyCallContext context, PyObject self, string name, out bool isMethod)
    {
        isMethod = false;
        if (!ReferenceEquals(self.PyType.Slots.GetAttribute, PyObjectType.GenericGetAttribute))
            return PyOperators.GetAttr(context, self, name);

        var type = self.PyType;

        if (name is PySpecialNames.Class)
            return type;

        if (name is PySpecialNames.Dict)
        {
            if (self.IsImmutable)
                return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.TpName, name);

            return self.PyAttributes.Self;
        }

        if (PyObject.TryLookupAttrInMro(type, name, out var attr))
        {
            if (PyUtils.IsDataDescriptor(attr))
            {
                var getFunc = attr.PyType.Slots.Get;
                if (getFunc is not null)
                    return getFunc(context, attr, self, type);
            }
        }

        if (self.PyAttributes.TryGetValue(name, out var value) is true)
            return value;

        if (attr is not null)
        {
            if (attr is PyFunctionObject or PyWrapperDescriptorObject)
            {
                isMethod = true;
                return attr;
            }

            var getFunc = attr.PyType.Slots.Get;
            if (getFunc is not null)
                return getFunc(context, attr, self, type);

            return attr;
        }

        var getAttrFunc = self.PyType.Slots.GetAttr;
        if (getAttrFunc is not null)
            return getAttrFunc(context, self, context.PyEnvironment.InternPool.Intern(name));

        return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.TpName, name);
    }
}
