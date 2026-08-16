using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PySharp.Runtime.VirtualMachine;

internal static partial class BytecodeVirtualMachine
{
    private static void InternalUnpackEx(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var postCount = instructionArg & ushort.MaxValue;
        var preCount = (instructionArg >> 16) & ushort.MaxValue;
        var list = PyUtils.IterableToList(context, stack.Pop()).PyUnwrap(context);
        var span = CollectionsMarshal.AsSpan(list.InternalList);
        if (span.Length < preCount + postCount)
            throw context.ValueError(PySR.Runtime_Assignment_NotEnoughToUnpackStarred, preCount + postCount, span.Length);
        stack.PushReversedRange(span[^postCount..]);
        stack.Push(PyListObject.CreateList(span[preCount..^postCount]));
        stack.PushReversedRange(span[..preCount]);
    }

    private static void InternalMatchClass(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var keys = (PyTupleObject)stack.Pop();
        var value = stack.Pop();
        if (value is not PyTypeObject cls)
            throw context.TypeError(PySR.Runtime_MatchStmt_CallNonClass);
        var subject = stack.Pop();

        if (!cls.IsInstance(subject))
        {
            stack.Push(PyNoneObject.None);
            return;
        }

        var values = new PyObject[instructionArg + keys.Count];

        if (IsSpecialType(cls))
        {
            if (instructionArg > 1)
                throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsLengthNotEnough, cls.FullName, 1, instructionArg);
            else if (instructionArg is 1)
                values[0] = subject;
        }
        else if (instructionArg > 0)
        {
            var matchArgs = PyOperators.GetAttr(context, cls, PySpecialNames.Interned.MatchArgs).PyUnwrap(context);

            if (matchArgs is not PyTupleObject tuple)
                throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsIsNonTuple, cls.FullName, matchArgs.PyType.FullName);
            if (instructionArg > tuple.Count)
                throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsLengthNotEnough, cls.FullName, tuple.Count, instructionArg);

            for (int i = 0; i < instructionArg; i++)
            {
                if (tuple[i] is not PyStrObject attrName)
                    throw context.TypeError(PySR.Runtime_MatchStmt_MatchArgsEltMustBeString, tuple[i].PyType.FullName);

                var attr = PyOperators.GetAttr(context, subject, attrName);
                if (attr.IsAttributeError)
                {
                    stack.Push(PyNoneObject.None);
                    return;
                }

                values[i] = attr.PyUnwrap(context);
            }
        }

        for (int i = 0; i < keys.Count; i++)
        {
            var attrName = keys[i];
            Debug.Assert(attrName is PyStrObject);

            var attr = PyOperators.GetAttr(context, subject, attrName);
            if (attr.IsAttributeError)
            {
                stack.Push(PyNoneObject.None);
                return;
            }

            values[instructionArg + i] = attr.PyUnwrap(context);
        }

        stack.Push(PyTupleObject.CreateProxy(values));

        static bool IsSpecialType(PyTypeObject type)
        {
            return type is
                PyBoolObjectType or
                PyByteArrayObjectType or
                PyBytesObjectType or
                PyDictObjectType or
                PyFloatObjectType or
                PyFrozenSetObjectType or
                PyIntObjectType or
                PyListObjectType or
                PySetObjectType or
                PyStrObjectType or
                PyTupleObjectType;
        }
    }

    private static void InternalMatchKeys(PyCallContext context, ref ValueOperandStack stack)
    {
        var keys = (PyTupleObject)stack.Peek();
        var subject = stack[-2];
        var array = new PyObject[keys.Count];
        var matched = true;
        for (int i = 0; matched && i < array.Length; i++)
        {
            var key = keys[i];
            var result = PySpecialMethods.GetItem(context, subject, key);
            if (result.IsError && PyKeyErrorObjectType.Shared.IsInstance(result.Exception))
            {
                matched = false;
                break;
            }
            array[i] = result.PyUnwrap(context);
        }
        stack.Push(matched ? PyTupleObject.CreateProxy(array) : PyNoneObject.None);
    }

    private static void InternalSend(PyCallContext context, ref BytecodeVirtualMachineStates states,
        ref ValueOperandStack stack, ref int nextIndex, int instructionArg)
    {
        PyObject iter, value;
        PyResult result;

        if (states.ExceptionToRaise is not null)
        {
            // throw or close

            iter = stack[-1];

            if (PyGeneratorExitObjectType.Shared.IsInstance(states.ExceptionToRaise))
            {
                // close sub generator
                var close = PyOperators.GetAttr(context, iter, "close");
                if (!close.IsAttributeError)
                    _ = close.PyUnwrap(context).Call(context).PyUnwrap(context);

                // close self
                if (states.ExceptionToRaise is not null)
                {
                    var exc = Move(ref states.ExceptionToRaise);
                    throw new PyRuntimeException(exc);
                }
            }
            else
            {
                var throwMethod = PyOperators.GetAttr(context, iter, "throw");
                if (!throwMethod.IsAttributeError)
                {
                    var exc = Move(ref states.ExceptionToRaise);
                    value = throwMethod.PyUnwrap(context).Call(context, [exc]).PyUnwrap(context);
                    stack.Push(value);
                }
                else
                {
                    // throw at self
                    if (states.ExceptionToRaise is not null)
                    {
                        var exc = Move(ref states.ExceptionToRaise);
                        throw new PyRuntimeException(exc);
                    }
                }
                return;
            }
        }

        iter = stack[-2];
        value = stack[-1];
        if (iter is PyGeneratorObject gen)
        {
            if (value is PyNoneObject)
                result = gen.PyNext(context);
            else
                result = gen.PySend(context, value);
        }
        else
        {
            if (value is PyNoneObject)
                result = PySpecialMethods.Next(context, iter);
            else
                result = iter.CallMethod(context, "send", [value]);
        }

        if (result.IsStopIteration)
        {
            // replace sent value with received value by 'yield from'
            stack[-1] = result.Exception.Args.FirstOrDefault(PyNoneObject.None);
            nextIndex = instructionArg;
        }
        else
        {
            stack[-1] = result.PyUnwrap(context);
        }
    }

    private static void InternalMapAdd(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var value = stack.Pop();
        var key = stack.Pop();
        var dict = (PyDictObject)stack[-instructionArg];
        dict[key] = value;
    }

    private static void InternalDictUpdate(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var map = stack.Pop();
        var dict = (PyDictObject)stack[-instructionArg];
        _ = dict.PyUpdate(context, map).PyUnwrap(context);
    }

    private static void InternalDictMerge(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var map = stack.Pop();
        var dictToMerge = PyUtils.ToDict(context, map).PyUnwrap(context);
        var dict = (PyDictObject)stack[-instructionArg];
        foreach (var pair in dictToMerge)
        {
            if (!dict.TryAdd(pair.Key, pair.Value))
                throw context.TypeError(PySR.Runtime_Arguments_MultipleKeywords, pair.Key);
        }
    }

    private static void InternalRaiseVarArgs(PyCallContext context, ref ValueOperandStack stack, ref BytecodeVirtualMachineStates states, int instructionArg)
    {
        if (instructionArg is 0)
        {
            PyCore.Raise(context, ref states, excObj: null, causeObj: null);
        }
        else if (instructionArg is 1)
        {
            var excObj = stack.Pop();
            PyCore.Raise(context, ref states, excObj, causeObj: null);
        }
        else if (instructionArg is 2)
        {
            var causeObj = stack.Pop();
            var excObj = stack.Pop();
            PyCore.Raise(context, ref states, excObj, causeObj);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    private static void InternalCheckEgMatch(PyCallContext context, ref ValueOperandStack stack, ref BytecodeVirtualMachineStates states, int instructionArg)
    {
        var exc = states.CurrentException;
        if (!exc.IsGroup)
            exc = PyBaseExceptionGroupObjectType.CreateExceptionGroup(string.Empty, [exc]);

        var type = stack.Pop();
        var (rest, match) = PyCore.SplitExceptionGroup(context, exc, type);
        states.Exceptions.Pop();
        states.ExceptionHandlers.Peek().PyException = rest;
        states.Exceptions.Push(rest! /* null if rest is None, OpCode._PopExceptionAndJumpIfNull should handle that */);
        stack.Push(match);
    }

    private static void InternalBuildSlice(ref ValueOperandStack stack, int instructionArg)
    {
        if (instructionArg is 2)
        {
            var end = stack.Pop();
            var start = stack.Pop();
            var slice = new PySliceObject(start, end, PyNoneObject.None);
            stack.Push(slice);
        }
        else
        {
            Debug.Assert(instructionArg is 3);
            var step = stack.Pop();
            var end = stack.Pop();
            var start = stack.Pop();
            var slice = new PySliceObject(start, end, step);
            stack.Push(slice);
        }
    }

    private static void InternalMakeFunctionWithPyArgsDef(ref PyInternalFrame frame, ref ValueOperandStack stack)
    {
        var codeObj = (PyCodeObject)stack.Pop();

        // Pop bundled defaults tuple and kwdefaults tuple.
        // null entries (from PushNull for missing kwdefaults) are stored directly
        // in the tuple's internal array — no sentinel needed.
        var kwDefaultsObj = stack.Pop();
        var kwDefaults = kwDefaultsObj is PyTupleObject kwdt
            ? Enumerable.Range(0, kwdt.Count).Select(i => kwdt[i]).ToArray()
            : [];

        var defaultsObj = stack.Pop();
        var defaults = defaultsObj is PyTupleObject dt
            ? Enumerable.Range(0, dt.Count).Select(i => dt[i]).ToArray()
            : [];

        var def = PyArgsDef.FromCodeObjectAndDefaults(codeObj, kwDefaults!, defaults!);
        var func = PyCore.MakeFunction(ref frame, codeObj, def);
        stack.Push(func);
    }

    private static void InternalSetupAnnotations(ref PyInternalFrame frame)
    {
        if (frame.FrameType is FrameType.Class)
        {
            var locals = frame.Variables.Locals;
            if (!locals.ContainsKey(PySpecialNames.Annotations))
                locals[PySpecialNames.Annotations] = new PyDictObject();
        }
        else if (frame.FrameType is FrameType.MainRoot or FrameType.Module)
        {
            var globals = frame.Variables.Globals;
            if (!globals.ContainsKey(PySpecialNames.Interned.Annotations))
                globals[PySpecialNames.Interned.Annotations] = new PyDictObject();
        }
    }

    private static void InternalBuildClass(PyCallContext context, ref ValueOperandStack stack, ref BytecodeVirtualMachineStates states, int instructionArg)
    {
        // Pop closure (PyNoneObject.None for non-generic; type_params PyTupleObject for generic)
        // Closure is pushed AFTER codeObj, so it's on top of stack
        var closure = (PyTupleObject?)stack.Pop();
        var codeObj = (PyCodeObject)stack.Pop();
        var tuple = (PyTupleObject)stack.Pop();

        int kwCount = tuple.Count;
        int basesCount = instructionArg - kwCount;

        states.CacheKwargs.Clear();
        var kwargs = states.CacheKwargs;
        if (kwCount > 0)
        {
            LoadArgs(ref stack, states.CacheArgs, kwCount);
            for (int i = 0; i < tuple.Count; i++)
            {
                var str = (PyStrObject)tuple[i];
                kwargs[str.Value] = states.CacheArgs[i];
            }
        }

        LoadArgs(ref stack, states.CacheArgs, basesCount);
        foreach (var arg in states.CacheArgs)
        {
            if (arg is not PyTypeObject baseType)
                throw context.PySharpException("non-type base is not supported");
        }

        var type = PyCore.BuildClass(context, codeObj, [.. states.CacheArgs.Cast<PyTypeObject>()], kwargs, closure);

        // closure is passed to the class body frame to populate free var cells;
        // __type_params__ is built inside the class body (via LoadDeref → BuildTuple → StoreName)
        // and becomes a class attribute through type.__new__.
        stack.Push(type);
    }

    private static void InternalUnpackSequence(PyCallContext context, ref ValueOperandStack stack, int instructionArg)
    {
        var list = PyUtils.IterableToList(context, stack.Pop()).PyUnwrap(context);
        var span = CollectionsMarshal.AsSpan(list.InternalList);
        if (span.Length > instructionArg)
            throw context.ValueError(PySR.Runtime_Assignment_TooManyToUnpack, instructionArg, span.Length);
        else if (span.Length < instructionArg)
            throw context.ValueError(PySR.Runtime_Assignment_NotEnoughToUnpack, instructionArg, span.Length);
        stack.PushReversedRange(span);
    }

    [AIGenerated]
    private static void InternalImportName(PyCallContext context, ref ValueOperandStack stack, ReadOnlySpan<string> names, int instructionArg)
    {
        var fromList = stack.Pop();
        var level = (PyIntObject)stack.Pop();

        var name = names[instructionArg];

        if (level.Value > 0)
        {
            // Relative import: resolve the name relative to the caller's package
            var callerGlobals = context.CurrentInternalFrame.Variables.Globals;
            callerGlobals.TryGetValue(PySpecialNames.Interned.Package, out var packageObj);
            var moduleName = ((PyStrObject)callerGlobals[PySpecialNames.Interned.Name]).Value;
            var hasPath = callerGlobals.ContainsKey(PySpecialNames.Interned.Path);
            name = PyEnvironment.ResolveRelativeModuleName(context, packageObj, moduleName, hasPath, name, level.Int32Value);
        }

        if (!context.PyEnvironment.TryLoadModule(context, name, out var rootModule, out var module))
            throw context.ModuleNotFoundError(PySR.Runtime_Import_ModuleNotFound, name);

        // If fromlist is non-empty, try to import each name as a submodule of the package
        // This mirrors CPython's _handle_fromlist: for from package import X, ensure X is
        // imported as a submodule if it exists as one
        bool hasFromList = fromList switch
        {
            PyNoneObject => false,
            PyTupleObject t => t.Count > 0,
            PyListObject l => l.Count > 0,
            _ => true
        };

        if (hasFromList)
        {
            var fromItems = fromList switch
            {
                PyTupleObject t => (IEnumerable<PyObject>)t,
                PyListObject l => l,
                _ => []
            };
            foreach (var item in fromItems)
            {
                if (item is not PyStrObject itemStr || module.PyAttributes.ContainsKey(itemStr.Value))
                    continue;

                // Try to load the submodule: module.Name + "." + item
                var subName = module.Name + '.' + itemStr.Value;
                if (context.PyEnvironment.TryLoadModule(context, subName, out _, out var subModule))
                    module.PyAttributes[itemStr.Value] = subModule;
            }
            stack.Push(module);
        }
        else
        {
            // fromList is None or empty: push the root module
            stack.Push(rootModule);
        }
    }
}
