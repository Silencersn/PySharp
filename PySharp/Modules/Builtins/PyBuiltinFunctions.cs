using PySharp.Compilation;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;
using System.Diagnostics;
using System.Text;

namespace PySharp.Modules.Builtins;

public static partial class PyBuiltinFunctions
{
    // A
    public static readonly PyBuiltinFunctionOrMethodObject Abs = PyBuiltinFunctionOrMethodObject.CreateFunction("abs", AbsImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Aiter = PyBuiltinFunctionOrMethodObject.CreateFunction("aiter", AiterImpl);
    public static readonly PyBuiltinFunctionOrMethodObject All = PyBuiltinFunctionOrMethodObject.CreateFunction("all", AllImpl);
    public static readonly PyBuiltinFunctionOrMethodObject ANext = PyBuiltinFunctionOrMethodObject.CreateFunction("anext", ANextImpl_1, ANextImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject Any = PyBuiltinFunctionOrMethodObject.CreateFunction("any", AnyImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Ascii = PyBuiltinFunctionOrMethodObject.CreateFunction("ascii", AsciiImpl);

    // B
    public static readonly PyBuiltinFunctionOrMethodObject Bin = PyBuiltinFunctionOrMethodObject.CreateFunction("bin", BinImpl);
    // bool -> PyBoolObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Breakpoint = PyBuiltinFunctionOrMethodObject.CreateFunction("breakpoint", BreakpointImpl);
    // bytearray -> PyByteArrayObjectType
    // bytes -> PyBytesObjectType

    // C
    public static readonly PyBuiltinFunctionOrMethodObject Callable = PyBuiltinFunctionOrMethodObject.CreateFunction("callable", CallableImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Chr = PyBuiltinFunctionOrMethodObject.CreateFunction("chr", ChrImpl);
    // classmethod -> PyClassMethodObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Compile = PyBuiltinFunctionOrMethodObject.CreateFunction("compile", CompileImpl);
    // complex -> PyComplexObjectType

    // D
    public static readonly PyBuiltinFunctionOrMethodObject DelAttr = PyBuiltinFunctionOrMethodObject.CreateFunction("delattr", DelAttrImpl);
    // dict -> PyDictObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Dir = PyBuiltinFunctionOrMethodObject.CreateFunction("dir", DirImpl_1, DirImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject DivMod = PyBuiltinFunctionOrMethodObject.CreateFunction("divmod", DivModImpl);

    // E
    // enumerate -> PyEnumerateObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Eval = PyBuiltinFunctionOrMethodObject.CreateFunction("eval", EvalImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Exec = PyBuiltinFunctionOrMethodObject.CreateFunction("exec", ExecImpl);

    // F
    // filter -> PyFilterObjectType
    // float -> PyFloatObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Format = PyBuiltinFunctionOrMethodObject.CreateFunction("format", FormatImpl);
    // frozenset -> PyFrozenSetObjectType

    // G
    public static readonly PyBuiltinFunctionOrMethodObject GetAttr = PyBuiltinFunctionOrMethodObject.CreateFunction("getattr", GetAttrImpl_1, GetAttrImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject Globals = PyBuiltinFunctionOrMethodObject.CreateFunction("globals", GlobalsImpl);

    // H
    public static readonly PyBuiltinFunctionOrMethodObject HasAttr = PyBuiltinFunctionOrMethodObject.CreateFunction("hasattr", HasAttrImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Hash = PyBuiltinFunctionOrMethodObject.CreateFunction("hash", HashImpl);
    // help -> injected by site module (PySiteFunctions.Help)
    public static readonly PyBuiltinFunctionOrMethodObject Hex = PyBuiltinFunctionOrMethodObject.CreateFunction("hex", HexImpl);

    // I
    public static readonly PyBuiltinFunctionOrMethodObject Id = PyBuiltinFunctionOrMethodObject.CreateFunction("id", IdImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Input = PyBuiltinFunctionOrMethodObject.CreateFunction("input", InputImpl_1, InputImpl_2);
    // int -> PyIntObjectType
    public static readonly PyBuiltinFunctionOrMethodObject IsInstance = PyBuiltinFunctionOrMethodObject.CreateFunction("isinstance", IsInstanceImpl);
    public static readonly PyBuiltinFunctionOrMethodObject IsSubclass = PyBuiltinFunctionOrMethodObject.CreateFunction("issubclass", IsSubclassImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Iter = PyBuiltinFunctionOrMethodObject.CreateFunction("iter", IterImpl);

    // L
    public static readonly PyBuiltinFunctionOrMethodObject Len = PyBuiltinFunctionOrMethodObject.CreateFunction("len", LenImpl);
    // list -> PyListObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Locals = PyBuiltinFunctionOrMethodObject.CreateFunction("locals", LocalsImpl);

    // M
    // map -> PyMapObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Max = PyBuiltinFunctionOrMethodObject.CreateFunction("max", MaxImpl_1, MaxImpl_2, MaxImpl_3);
    // memoryview -> PyMemoryViewObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Min = PyBuiltinFunctionOrMethodObject.CreateFunction("min", MinImpl_1, MinImpl_2, MinImpl_3);

    // N
    public static readonly PyBuiltinFunctionOrMethodObject Next = PyBuiltinFunctionOrMethodObject.CreateFunction("next", NextImpl_1, NextImpl_2);


    // O
    // object -> PyObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Oct = PyBuiltinFunctionOrMethodObject.CreateFunction("oct", OctImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Open = PyBuiltinFunctionOrMethodObject.CreateFunction("open", OpenImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Ord = PyBuiltinFunctionOrMethodObject.CreateFunction("ord", OrdImpl);

    // P
    public static readonly PyBuiltinFunctionOrMethodObject Pow = PyBuiltinFunctionOrMethodObject.CreateFunction("pow", PowImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Print = PyBuiltinFunctionOrMethodObject.CreateFunction("print", PrintImpl);
    // property -> PyPropertyObjectType

    // R
    // range -> PyRangeObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Repr = PyBuiltinFunctionOrMethodObject.CreateFunction("repr", ReprImpl);
    // reversed -> PyReversedObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Round = PyBuiltinFunctionOrMethodObject.CreateFunction("round", RoundImpl_1, RoundImpl_2);

    // S
    // set -> PySetObjectType
    public static readonly PyBuiltinFunctionOrMethodObject SetAttr = PyBuiltinFunctionOrMethodObject.CreateFunction("setattr", SetAttrImpl);
    // slice -> PySliceObjectType
    public static readonly PyBuiltinFunctionOrMethodObject Sorted = PyBuiltinFunctionOrMethodObject.CreateFunction("sorted", SortedImpl);
    // staticmethod -> PyStaticMethodObjectType
    // str -> PyStrObject
    public static readonly PyBuiltinFunctionOrMethodObject Sum = PyBuiltinFunctionOrMethodObject.CreateFunction("sum", SumImpl);
    // super -> PySuperObjectType

    // T
    // tuple -> PyTupleObjectType
    // type -> PyTypeObjectType

    // V
    public static readonly PyBuiltinFunctionOrMethodObject Vars = PyBuiltinFunctionOrMethodObject.CreateFunction("vars", VarsImpl_1, VarsImpl_2);

    // Z
    // zip -> PyZipObjectType

    // _
    public static readonly PyBuiltinFunctionOrMethodObject Import = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.Import, ImportImpl);


    [PyFunctionParameters("*objects", "sep=' '", "end='\\n'", "file=None", "flush=False")]
    private static PyResult PrintImpl(PyCallContext context, PyArguments arguments)
    {
        var sepObj = arguments.GetKwargByIndex(0);
        if (!Utils.TryGetValue(sepObj, (PyStrObject str) => str.Value, " ", out var sep))
            return PyResult.TypeError(PySR.Runtime_Builtin_Print_WrongArgType, "sep", sepObj.PyType.FullName);

        var endObj = arguments.GetKwargByIndex(1);
        if (!Utils.TryGetValue(endObj, (PyStrObject str) => str.Value, "\n", out var end))
            return PyResult.TypeError(PySR.Runtime_Builtin_Print_WrongArgType, "end", endObj.PyType.FullName);

        var fileObj = arguments.GetKwargByIndex(2);

        var flushResult = PySpecialMethods.Bool(context, arguments.GetKwargByIndex(3));
        if (flushResult.IsError)
            return flushResult;

        if (fileObj is PyNoneObject)
        {
            for (int i = 0; i < arguments.ExtraArgs.Count; i++)
            {
                if (i is not 0)
                    context.Out.Write(sep);

                var strResult = PySpecialMethods.Str(context, arguments.ExtraArgs[i]);
                if (strResult.IsError)
                    return strResult;
                context.Out.Write(strResult.Value.Value);
            }
            context.Out.Write(end);
            if (flushResult.Value.BoolValue)
                context.Out.Flush();
        }
        else
        {
            PyResult WriteToFile(string text) => fileObj.CallMethod(context, "write", [PyStrObject.FromString(text)]);

            for (int i = 0; i < arguments.ExtraArgs.Count; i++)
            {
                if (i is not 0)
                {
                    var sepResult = WriteToFile(sep);
                    if (sepResult.IsError)
                        return sepResult;
                }

                var strResult = PySpecialMethods.Str(context, arguments.ExtraArgs[i]);
                if (strResult.IsError)
                    return strResult;
                var writeResult = WriteToFile(strResult.Value.Value);
                if (writeResult.IsError)
                    return writeResult;
            }

            var endResult = WriteToFile(end);
            if (endResult.IsError)
                return endResult;

            if (flushResult.Value.BoolValue)
            {
                var flushCall = fileObj.CallMethod(context, "flush");
                if (flushCall.IsError)
                    return flushCall;
            }
        }

        return PyNoneObject.None;
    }

    [PyFunctionParameters("base", "exp", "mod=None")]
    private static PyResult PowImpl(PyCallContext context, PyArguments arguments)
    {
        var baseObj = arguments[0];
        var expObj = arguments[1];
        var modObj = arguments[2];

        var result = PyOperators.Pow(context, baseObj, expObj, modObj);
        if (result.IsError)
            return result;

        Debug.Assert(!result.IsNotImplemented);
        return result;
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult DivModImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.DivMod(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters()]
    private static PyResult InputImpl_1(PyCallContext context, PyArguments arguments)
    {
        var str = PyStrObject.FromString(context.In.ReadLine() ?? string.Empty);
        return str;
    }
    [PyFunctionParameters("prompt", "/")]
    private static PyResult InputImpl_2(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Str(context, arguments[0]);
        if (result.IsError)
            return result;

        context.Out.Write(result.Value.Value);
        var str = PyStrObject.FromString(context.In.ReadLine() ?? string.Empty);
        return str;
    }
    [PyFunctionParameters("source", "/", "globals=None", "locals=None")]
    private static PyResult EvalImpl(PyCallContext context, PyArguments arguments)
    {
        var source = arguments[0];
        if (source is not PyStrObject && source is not PyBytesObject && source is not PyCodeObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Arg1WrongType, "eval");

        if (source is PyBytesObject bytesSource)
            source = PyStrObject.FromString(Encoding.UTF8.GetString(bytesSource.AsSpan()));

        if (source is PyCodeObject { FreeVars.Length: > 0 })
            return PyResult.TypeError(PySR.Runtime_Builtin_Eval_PassCodeObjWithFreeVars);

        var globals = arguments[1];
        var globalsDict = globals as PyDictObject;
        if (globalsDict is null && globals is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Globals);

        var locals = arguments[2];
        var localsDict = locals as PyDictObject;
        if (localsDict is null && locals is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Locals);

        ref var frame = ref context.CurrentInternalFrame;

        if (source is PyCodeObject code)
        {
            var newFrame = frame.CreateExecEvalFrame(FrameType.Eval, globalsDict, localsDict, code);
            using var withFrame = context.WithFrame(ref newFrame);
            return PyCore.Eval(context);
        }

        Debug.Assert(source is PyStrObject);

        try
        {
            var newFrame = frame.CreateExecEvalFrame(FrameType.Eval, globalsDict, localsDict);
            using var withFrame = context.WithFrame(ref newFrame);
            var codeObj = Compiler.InternalCompileEval(context, ((PyStrObject)source).Value, filename: "<string>", name: "<module>", onlyAsName: true);
            context.CurrentInternalFrame.CodeObject = codeObj;
            return PyCore.Eval(context);
        }
        catch (PyRuntimeException e)
        {
            e.PyException.WithTraceback(context, overwriteExisting: false);
            return PyResult.FromException(e.PyException);
        }
    }
    [PyFunctionParameters("source", "/", "globals=None", "locals=None", "*", "closure=None")]
    private static PyResult ExecImpl(PyCallContext context, PyArguments arguments)
    {
        var source = arguments[0];
        if (source is not PyStrObject && source is not PyBytesObject && source is not PyCodeObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Arg1WrongType, "exec");

        if (source is PyBytesObject bytesSource)
            source = PyStrObject.FromString(Encoding.UTF8.GetString(bytesSource.AsSpan()));

        var globals = arguments[1];
        var globalsDict = globals as PyDictObject;
        if (globalsDict is null && globals is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Globals);

        var locals = arguments[2];
        var localsDict = locals as PyDictObject;
        if (localsDict is null && locals is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Locals);

        var closure = arguments.GetKwargByIndex(0);
        if (closure is not PyNoneObject && source is not PyCodeObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_Exec_ClosureForNonCodeObj);

        ref var frame = ref context.CurrentInternalFrame;

        if (source is PyCodeObject code)
        {
            if (closure is not PyNoneObject && code.FreeVars.Length is 0)
                return PyResult.TypeError(PySR.Runtime_Builtin_Exec_CannotUseClosure);

            var closureTuple = closure as PyTupleObject;
            if (!(closure is PyNoneObject ||
                closureTuple is not null &&
                closureTuple.Count == code.FreeVars.Length &&
                closureTuple.All(static obj => obj is PyCellObject)))
                return PyResult.TypeError(PySR.Runtime_Builtin_Exec_WrongClosure, code.FreeVars.Length);

            Debug.Assert(code.Bytecode is not null);
            var newFrame = frame.CreateExecEvalFrame(FrameType.Exec, globalsDict, localsDict, code, closureTuple);
            using var withFrame = context.WithFrame(ref newFrame);
            return PyCore.Eval(context);
        }

        Debug.Assert(closure is PyNoneObject);
        Debug.Assert(source is PyStrObject);

        try
        {
            var newFrame = frame.CreateExecEvalFrame(FrameType.Exec, globalsDict, localsDict);
            using var withFrame = context.WithFrame(ref newFrame);
            var codeObj = Compiler.InternalCompileExec(context, ((PyStrObject)source).Value, filename: "<string>", name: "<module>", onlyAsName: true);
            context.CurrentInternalFrame.CodeObject = codeObj;
            return PyCore.Eval(context);
        }
        catch (PyRuntimeException e)
        {
            e.PyException.WithTraceback(context, overwriteExisting: false);
            return PyResult.FromException(e.PyException);
        }
    }

    [PyFunctionParameters("async_iterable", "/")]
    private static PyResult AiterImpl(PyCallContext context, PyArguments arguments)
    {
        var obj = arguments[0];
        var slot = obj.PyType.Slots.AIter;
        if (slot is null)
            return PyResult.TypeError(PySR.Runtime_Builtin_Aiter_NotAsyncIterable, obj.PyType.FullName);
        return slot(context, obj);
    }

    [PyFunctionParameters("async_iterator", "/")]
    private static PyResult ANextImpl_1(PyCallContext context, PyArguments arguments)
    {
        var obj = arguments[0];
        var slot = obj.PyType.Slots.ANext;
        if (slot is null)
            return PyResult.TypeError(PySR.Runtime_Builtin_ANext_NotAsyncIterator, obj.PyType.FullName);
        return slot(context, obj);
    }

    [PyFunctionParameters("async_iterator", "default", "/")]
    private static PyResult ANextImpl_2(PyCallContext context, PyArguments arguments)
    {
        return new PyAnextAwaitableObject(arguments[0], arguments[1]);
    }

    [PyFunctionParameters("iterable")]
    private static PyResult AllImpl(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (!Utils.TryEnumerateIterable(context, iterable, out var elements, out var err))
            return err.Value;
        foreach (var element in elements)
        {
            if (element.IsError)
                return element;
            var result = PySpecialMethods.Bool(context, element.Value);
            if (result.IsError)
                return result;
            if (!result.Value.BoolValue)
                return PyBoolObject.False;
        }
        return PyBoolObject.True;
    }

    [PyFunctionParameters("iterable")]
    private static PyResult AnyImpl(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (!Utils.TryEnumerateIterable(context, iterable, out var elements, out var err))
            return err.Value;
        foreach (var element in elements)
        {
            if (element.IsError)
                return element;
            var result = PySpecialMethods.Bool(context, element.Value);
            if (result.IsError)
                return result;
            if (result.Value.BoolValue)
                return PyBoolObject.True;
        }
        return PyBoolObject.False;
    }

    private static PyResult GetMaxMinKey(PyCallContext context, PyObject keySelector, PyObject item)
    {
        if (keySelector is PyNoneObject)
            return item;
        return keySelector.Call(context, [item]);
    }

    [PyFunctionParameters("iterable", "/", "*", "key=None")]
    private static PyResult MaxImpl_1(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        var keySelector = arguments.GetKwargByIndex(0);

        var elements = PyUtils.IterableToList(context, iterable);
        if (elements.IsError)
            return elements;

        PyObject? result = null;
        PyObject? resultKey = null;
        foreach (var element in elements.Value)
        {
            var keyResult = GetMaxMinKey(context, keySelector, element);
            if (keyResult.IsError)
                return keyResult;
            var key = keyResult.Value;

            if (result is null)
            {
                result = element;
                resultKey = key;
                continue;
            }

            var gt = PyOperators.Gt(context, key, resultKey!);
            if (gt.IsError)
                return gt;
            var bResult = PySpecialMethods.Bool(context, gt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
            {
                result = element;
                resultKey = key;
            }
        }
        if (result is null)
            return PyResult.ValueError(PySR.Runtime_Builtin_Max_EmptyIterable);
        return result;
    }

    [PyFunctionParameters("iterable", "/", "*", "default", "key=None")]
    private static PyResult MaxImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        var defaultObj = arguments.GetKwargByIndex(0);
        var keySelector = arguments.GetKwargByIndex(1);

        var elements = PyUtils.IterableToList(context, iterable);
        if (elements.IsError)
            return elements;

        PyObject? result = null;
        PyObject? resultKey = null;
        foreach (var element in elements.Value)
        {
            var keyResult = GetMaxMinKey(context, keySelector, element);
            if (keyResult.IsError)
                return keyResult;
            var key = keyResult.Value;

            if (result is null)
            {
                result = element;
                resultKey = key;
                continue;
            }

            var gt = PyOperators.Gt(context, key, resultKey!);
            if (gt.IsError)
                return gt;
            var bResult = PySpecialMethods.Bool(context, gt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
            {
                result = element;
                resultKey = key;
            }
        }
        // default is not compared against elements; returned only for an empty iterable
        return result ?? defaultObj;
    }

    [PyFunctionParameters("arg1", "arg2", "/", "*args", "key=None")]
    private static PyResult MaxImpl_3(PyCallContext context, PyArguments arguments)
    {
        var keySelector = arguments.GetKwargByIndex(0);

        PyObject result = arguments[0];
        var resultKey = GetMaxMinKey(context, keySelector, result);
        if (resultKey.IsError)
            return resultKey;

        foreach (var element in arguments.ExtraArgs.Prepend(arguments[1]))
        {
            var keyResult = GetMaxMinKey(context, keySelector, element);
            if (keyResult.IsError)
                return keyResult;
            var key = keyResult.Value;

            var gt = PyOperators.Gt(context, key, resultKey.Value);
            if (gt.IsError)
                return gt;
            var bResult = PySpecialMethods.Bool(context, gt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
            {
                result = element;
                resultKey = keyResult;
            }
        }
        return result;
    }

    [PyFunctionParameters("iterable", "/", "*", "key=None")]
    private static PyResult MinImpl_1(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        var keySelector = arguments.GetKwargByIndex(0);

        var elements = PyUtils.IterableToList(context, iterable);
        if (elements.IsError)
            return elements;

        PyObject? result = null;
        PyObject? resultKey = null;
        foreach (var element in elements.Value)
        {
            var keyResult = GetMaxMinKey(context, keySelector, element);
            if (keyResult.IsError)
                return keyResult;
            var key = keyResult.Value;

            if (result is null)
            {
                result = element;
                resultKey = key;
                continue;
            }

            var lt = PyOperators.Lt(context, key, resultKey!);
            if (lt.IsError)
                return lt;
            var bResult = PySpecialMethods.Bool(context, lt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
            {
                result = element;
                resultKey = key;
            }
        }
        if (result is null)
            return PyResult.ValueError(PySR.Runtime_Builtin_Min_EmptyIterable);
        return result;
    }

    [PyFunctionParameters("iterable", "/", "*", "default", "key=None")]
    private static PyResult MinImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        var defaultObj = arguments.GetKwargByIndex(0);
        var keySelector = arguments.GetKwargByIndex(1);

        var elements = PyUtils.IterableToList(context, iterable);
        if (elements.IsError)
            return elements;

        PyObject? result = null;
        PyObject? resultKey = null;
        foreach (var element in elements.Value)
        {
            var keyResult = GetMaxMinKey(context, keySelector, element);
            if (keyResult.IsError)
                return keyResult;
            var key = keyResult.Value;

            if (result is null)
            {
                result = element;
                resultKey = key;
                continue;
            }

            var lt = PyOperators.Lt(context, key, resultKey!);
            if (lt.IsError)
                return lt;
            var bResult = PySpecialMethods.Bool(context, lt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
            {
                result = element;
                resultKey = key;
            }
        }
        // default is not compared against elements; returned only for an empty iterable
        return result ?? defaultObj;
    }

    [PyFunctionParameters("arg1", "arg2", "/", "*args", "key=None")]
    private static PyResult MinImpl_3(PyCallContext context, PyArguments arguments)
    {
        var keySelector = arguments.GetKwargByIndex(0);

        PyObject result = arguments[0];
        var resultKey = GetMaxMinKey(context, keySelector, result);
        if (resultKey.IsError)
            return resultKey;

        foreach (var element in arguments.ExtraArgs.Prepend(arguments[1]))
        {
            var keyResult = GetMaxMinKey(context, keySelector, element);
            if (keyResult.IsError)
                return keyResult;
            var key = keyResult.Value;

            var lt = PyOperators.Lt(context, key, resultKey.Value);
            if (lt.IsError)
                return lt;
            var bResult = PySpecialMethods.Bool(context, lt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
            {
                result = element;
                resultKey = keyResult;
            }
        }
        return result;
    }

    [PyFunctionParameters("iterable", "/", "start=0")]
    private static PyResult SumImpl(PyCallContext context, PyArguments arguments)
    {
        var start = arguments[1];
        if (start is PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_Sum_Strings);

        var list = PyUtils.IterableToList(context, arguments[0]);
        if (list.IsError)
            return list;

        var result = start;
        foreach (var item in list.Value)
        {
            var ret = PyOperators.Add(context, result, item);
            if (ret.IsError)
                return ret;
            result = ret.Value;
        }
        return result;
    }

    [PyFunctionParameters("object", "name", "/")]
    private static PyResult GetAttrImpl_1(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.GetAttr(context, arguments[0], arguments[1]);
    }

    [PyFunctionParameters("object", "name", "default", "/")]
    private static PyResult GetAttrImpl_2(PyCallContext context, PyArguments arguments)
    {
        var attr = PyOperators.GetAttr(context, arguments[0], arguments[1]);
        if (!attr.IsAttributeError)
            return attr;
        return arguments[2];
    }

    [PyFunctionParameters("object", "name", "value", "/")]
    private static PyResult SetAttrImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.SetAttr(context, arguments[0], arguments[1], arguments[2]);
    }

    [PyFunctionParameters("object", "name", "/")]
    private static PyResult HasAttrImpl(PyCallContext context, PyArguments arguments)
    {
        var attr = PyOperators.GetAttr(context, arguments[0], arguments[1]);
        if (attr.IsSuccessful)
            return PyBoolObject.True;
        if (attr.IsAttributeError)
            return PyBoolObject.False;
        return attr;
    }

    [PyFunctionParameters()]
    private static PyResult DirImpl_1(PyCallContext context, PyArguments arguments)
    {
        var result = PyListObject.CreateList(context.CurrentInternalFrame.Variables
            .EnumerateLocals()
            .Select(static pair => PyStrObject.FromString(pair.Key)));
        return result;
    }
    [PyFunctionParameters("object", "/")]
    private static PyResult DirImpl_2(PyCallContext context, PyArguments arguments)
    {
        List<string> attrs = [];
        var obj = arguments[0];
        attrs.AddRange(obj.PyAttributes.Keys);
        foreach (var type in obj.PyType.MRO)
            attrs.AddRange(type.PyAttributes.Keys);
        var result = PyListObject.CreateList(attrs.Distinct().Order().Select(PyStrObject.FromString));
        return result;
    }

    [PyFunctionParameters("codepoint", "/")]
    private static PyResult ChrImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        var value = result.Value.Value;
        if (value < 0 || value > 0x10FFFF)
            return PyResult.ValueError(PySR.Runtime_Builtin_Chr_OutOfRange);
        int cp = (int)value;
        // CPython allows lone surrogates: chr(0xD800) -> '\ud800' (len 1)
        return cp <= 0xFFFF
            ? PyStrObject.FromString(((char)cp).ToString())
            : PyStrObject.FromRune(new Rune(cp));
    }

    [PyFunctionParameters("c", "/")]
    private static PyResult OrdImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyResult.TypeError(PySR.Runtime_Builtin_Ord_ExpectedString, arguments[0].PyType.Name);
        if (strObj.PyLength is not 1)
            return PyResult.TypeError(PySR.Runtime_Builtin_Ord_ExpectedACharacter, strObj.PyLength);
        return PyIntObject.FromInteger(strObj.PyCharAt(0).Value);
    }

    [PyFunctionParameters()]
    private static PyResult LocalsImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyDictObject.CreateDict(context.CurrentInternalFrame.Variables
            .EnumerateLocals()
            .Select(static pair => KeyValuePair.Create((PyObject)PyStrObject.FromString(pair.Key), pair.Value)));
        return result;
    }

    [PyFunctionParameters()]
    private static PyResult GlobalsImpl(PyCallContext context, PyArguments arguments)
    {
        var result = context.CurrentInternalFrame.Variables.Globals.PyDict;
        return result;
    }

    [AIGenerated]
    [PyFunctionParameters("name", "globals=None", "locals=None", "fromlist=()", "level=0")]
    private static PyResult ImportImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyResult.TypeError(PySR.Runtime_Builtin_Import_NameMustBeString);
        var name = strObj.Value;

        // Handle relative imports (level > 0)
        var levelArg = arguments[4];
        if (levelArg is PyIntObject levelObj && levelObj.Value > 0)
        {
            PyObject? packageObj;
            string moduleName;
            bool hasPath;

            var globalsArg = arguments[1];
            if (globalsArg is PyNoneObject)
            {
                // Use the current frame's globals
                var globals = context.CurrentInternalFrame.Variables.Globals.Dict;
                globals.TryGetValue(PySpecialNames.Package, out packageObj);
                if (!globals.TryGetValue(PySpecialNames.Name, out var nameObj) || nameObj is not PyStrObject nameStr)
                    return PyResult.TypeError(PySR.Runtime_Builtin_Import_NameMustBeString);
                moduleName = nameStr.Value;
                hasPath = globals.ContainsKey(PySpecialNames.Path);
            }
            else if (globalsArg is PyDictObject globalsDict)
            {
                // Extract values from the provided PyDictObject
                var nameKey = PyStrObject.FromString(PySpecialNames.Name);
                var packageKey = PyStrObject.FromString(PySpecialNames.Package);
                var pathKey = PyStrObject.FromString(PySpecialNames.Path);
                globalsDict.TryGetValue(packageKey, out packageObj);
                if (!globalsDict.TryGetValue(nameKey, out var nameObj) || nameObj is not PyStrObject nameStr)
                    return PyResult.TypeError(PySR.Runtime_Builtin_Import_NameMustBeString);
                moduleName = nameStr.Value;
                hasPath = globalsDict.ContainsKey(pathKey);
            }
            else
            {
                return PyResult.TypeError(PySR.Runtime_Builtin_Import_GlobalsMustBeDict);
            }

            // ResolveRelativeModuleName may throw PyRuntimeException;
            // convert to PyResult to keep error-return consistency within this method
            try
            {
                name = PyEnvironment.ResolveRelativeModuleName(context, packageObj, moduleName, hasPath, name, levelObj.Int32Value);
            }
            catch (PyRuntimeException ex)
            {
                return PyResult.FromException(ex.PyException);
            }
        }

        if (!context.PyEnvironment.TryLoadModule(context, name, out var rootModule, out var module))
            return PyResult.ModuleNotFoundError(PySR.Runtime_Import_ModuleNotFound, name);

        var fromList = arguments[3];

        // _handle_fromlist: when fromlist is non-empty and the module is a package,
        // try to import each name as a submodule (mirrors CPython behavior)
        bool hasFromList = fromList switch
        {
            PyNoneObject => false,
            PyTupleObject t => t.Count > 0,
            PyListObject l => l.Count > 0,
            _ => true
        };

        if (hasFromList)
        {
            foreach (var item in fromList switch
            {
                PyTupleObject t => (IEnumerable<PyObject>)t,
                PyListObject l => l,
                _ => []
            })
            {
                if (item is PyStrObject itemStr && !module.PyAttributes.ContainsKey(itemStr.Value))
                {
                    var subName = module.Name + '.' + itemStr.Value;
                    if (context.PyEnvironment.TryLoadModule(context, subName, out _, out var subModule))
                        module.PyAttributes[itemStr.Value] = subModule;
                }
            }
            return module;
        }

        return rootModule;
    }

    [PyFunctionParameters("object", "classinfo", "/")]
    private static PyResult IsInstanceImpl(PyCallContext context, PyArguments arguments)
    {
        var ret = IsInstanceForUnknown(arguments[0], arguments[1]);
        if (ret is null)
            return PyResult.TypeError(PySR.Runtime_Builtin_IsInstance_MustBeTypeOrTupleOfTypes);
        return PyBoolObject.FromBoolean(ret.Value);

        static bool? IsInstanceForUnknown(PyObject obj, PyObject classInfo)
        {
            return classInfo switch
            {
                PyTypeObject type => IsInstanceForType(obj, type),
                PyTupleObject types => IsInstanceForTuple(obj, types),
                _ => null
            };
        }

        static bool? IsInstanceForType(PyObject obj, PyTypeObject type)
        {
            return type.IsInstance(obj);
        }

        static bool? IsInstanceForTuple(PyObject obj, PyTupleObject types)
        {
            foreach (var type in types)
            {
                var ret = IsInstanceForUnknown(obj, type);
                if (ret is null or true)
                    return ret;
            }
            return false;
        }
    }

    [PyFunctionParameters("class", "classinfo", "/")]
    private static PyResult IsSubclassImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyTypeObject typeObj)
            return PyResult.TypeError(PySR.Runtime_Builtin_IsSubclass_Arg1MustBeClass);
        var ret = IsSubclassForUnknown(typeObj, arguments[1]);
        if (ret is null)
            return PyResult.TypeError(PySR.Runtime_Builtin_IsSubclass_Arg2MustBeTypeOrTupleOfTypes);
        return PyBoolObject.FromBoolean(ret.Value);

        static bool? IsSubclassForUnknown(PyTypeObject obj, PyObject classInfo)
        {
            return classInfo switch
            {
                PyTypeObject type => IsSubclassForType(obj, type),
                PyTupleObject types => IsSubclassForTuple(obj, types),
                _ => null
            };
        }

        static bool? IsSubclassForType(PyTypeObject obj, PyTypeObject type)
        {
            return obj.IsSubclassOf(type);
        }

        static bool? IsSubclassForTuple(PyTypeObject obj, PyTupleObject types)
        {
            foreach (var type in types)
            {
                var ret = IsSubclassForUnknown(obj, type);
                if (ret is null or true)
                    return ret;
            }
            return false;
        }
    }

    [PyFunctionParameters("object", "/")]
    private static PyResult CallableImpl(PyCallContext context, PyArguments arguments)
    {
        var attr = PyOperators.GetAttr(context, arguments[0], PySpecialNames.Interned.Call);
        if (attr.IsSuccessful)
            return PyBoolObject.True;
        if (attr.IsAttributeError)
            return PyBoolObject.False;
        return attr;
    }

    [PyFunctionParameters("object", "/")]
    private static PyResult IdImpl(PyCallContext context, PyArguments arguments)
    {
        return PyIntObject.FromInteger(arguments[0].PyId);
    }

    [PyFunctionParameters("object", "/")]
    private static PyResult HashImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Hash(context, arguments[0]);
    }

    [PyFunctionParameters("object", "/")]
    private static PyResult IterImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Iter(context, arguments[0]);
    }

    [PyFunctionParameters("object", "/")]
    private static PyResult LenImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Len(context, arguments[0]);
    }

    [PyFunctionParameters("iterator", "/")]
    private static PyResult NextImpl_1(PyCallContext context, PyArguments arguments)
    {
        var iterator = arguments[0];
        return PySpecialMethods.Next(context, iterator);
    }

    [PyFunctionParameters("iterator", "default", "/")]
    private static PyResult NextImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterator = arguments[0];
        var result = PySpecialMethods.Next(context, iterator);
        if (result.IsStopIteration)
            return arguments[1];
        return result;
    }

    [PyFunctionParameters("object", "/")]
    private static PyResult ReprImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Repr(context, arguments[0]);
    }


    [PyFunctionParameters("x", "/")]
    private static PyResult AbsImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Abs(context, arguments[0]);
    }

    [PyFunctionParameters("integer", "/")]
    private static PyResult BinImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;

        var value = BigIntegerHelper.ToString(result.Value.Value, 2);
        return PyStrObject.FromString(value);
    }

    [PyFunctionParameters("*args", "**kws")]
    private static PyResult BreakpointImpl(PyCallContext context, PyArguments arguments)
    {
        // CPython: breakpoint(*args, **kws) calls sys.breakpointhook(*args, **kws),
        // which by default invokes pdb.set_trace(). PySharp has no debugger
        // integration yet, so this is a no-op placeholder.
        return PyNoneObject.None;
    }

    [PyFunctionParameters("integer", "/")]
    private static PyResult OctImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;

        var value = BigIntegerHelper.ToString(result.Value.Value, 8);
        return PyStrObject.FromString(value);
    }

    [PyFunctionParameters("integer", "/")]
    private static PyResult HexImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;

        var value = BigIntegerHelper.ToString(result.Value.Value, 16);
        return PyStrObject.FromString(value);
    }

    [PyFunctionParameters("object", "/")]
    private static PyResult AsciiImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Repr(context, arguments[0]);
        if (result.IsError)
            return result;

        var repr = result.Value.Value;
        if (repr.EnumerateRunes().All(static rune => rune.IsAscii))
            return result.Value;

        var builder = new StringBuilder();

        foreach (var rune in repr.EnumerateRunes())
        {
            var ch = rune.Value;
            if (rune.IsAscii)
                builder.Append(rune.ToString());
            else if (ch < 0x100)
                builder.AppendFormat("\\x{0:x2}", ch);
            else if (ch < 0x10000)
                builder.AppendFormat("\\u{0:x4}", ch);
            else
                builder.AppendFormat("\\U{0:x8}", ch);
        }

        return PyStrObject.FromString(builder.ToString());
    }

    [PyFunctionParameters("value", "format_spec=''", "/")]
    private static PyResult FormatImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Format(context, arguments[0], arguments[1]);
    }

    [PyFunctionParameters("object", "name", "/")]
    private static PyResult DelAttrImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.DelAttr(context, arguments[0], arguments[1]);
    }

    [PyFunctionParameters("source", "filename", "mode" /* flags=0, dont_inherit=False, optimize=-1 */)]
    private static PyResult CompileImpl(PyCallContext context, PyArguments arguments)
    {
        string sourceStr;
        if (arguments[0] is PyStrObject source)
            sourceStr = source.Value;
        else if (arguments[0] is PyBytesObject sourceBytes)
            sourceStr = Encoding.UTF8.GetString(sourceBytes.AsSpan());
        else
            // TODO: ast
            return PyResult.TypeError(PySR.Runtime_Builtin_Compile_Arg1WrongType);

        string filenameStr;
        if (arguments[1] is PyStrObject filename)
            filenameStr = filename.Value;
        else
            return PyResult.TypeError(PySR.Runtime_Builtin_Compile_FilenameWrongType, arguments[1].PyType.FullName);

        string modeStr;
        if (arguments[2] is PyStrObject mode)
            modeStr = mode.Value;
        else
            return PyResult.TypeError(PySR.Runtime_Builtin_Compile_ModeWrongType, arguments[2].PyType.FullName);

        var codeObject = modeStr switch
        {
            "exec" => Compiler.InternalCompileExec(context, sourceStr, filenameStr, name: "<module>", onlyAsName: true),
            "eval" => Compiler.InternalCompileEval(context, sourceStr, filenameStr, name: "<module>", onlyAsName: true),
            "single" => Compiler.InternalCompileSingle(context, sourceStr, filenameStr, name: "<module>", appendNewLine: false, onlyAsName: true),
            _ => null
        };

        if (codeObject is null)
            return PyResult.TypeError(PySR.Runtime_Builtin_Compile_WrongMode);

        return codeObject;
    }

    [PyFunctionParameters("iterable", "/", "*", "key=None", "reverse=False")]
    private static PyResult SortedImpl(PyCallContext context, PyArguments arguments)
    {
        var list = PyUtils.IterableToList(context, arguments[0]);
        if (list.IsError)
            return list;

        var result = list.Value.PySort(context, arguments.GetKwargByIndex(0), arguments.GetKwargByIndex(1));
        if (result.IsError)
            return result;

        return list.Value;
    }

    [AIGenerated]
    [PyFunctionParameters("number", "/")]
    private static PyResult RoundImpl_1(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Round(context, arguments[0], PyNoneObject.None);
    }

    [AIGenerated]
    [PyFunctionParameters("number", "ndigits", "/")]
    private static PyResult RoundImpl_2(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Round(context, arguments[0], arguments[1]);
    }

    [PyFunctionParameters()]
    private static PyResult VarsImpl_1(PyCallContext context, PyArguments arguments)
    {
        return LocalsImpl(context, arguments);
    }

    [AIGenerated]
    [PyFunctionParameters("object", "/")]
    private static PyResult VarsImpl_2(PyCallContext context, PyArguments arguments)
    {
        var obj = arguments[0];
        return PyDictObject.CreateProxy(new DictAdapter(obj.PyAttributes!));
    }

    [PyFunctionParameters("file", "mode='r'")]
    private static PyResult OpenImpl(PyCallContext context, PyArguments arguments)
    {
        var fileObj = arguments[0];
        var modeObj = arguments[1];

        string path;
        if (fileObj is PyStrObject pathStr)
            path = pathStr.Value;
        else
            return PyResult.TypeError(PySR.Runtime_Builtin_Open_Arg1Type, fileObj.PyType.FullName);

        string mode;
        if (modeObj is PyStrObject modeStr)
            mode = modeStr.Value;
        else
            return PyResult.TypeError(PySR.Runtime_Builtin_Open_Arg2Type, modeObj.PyType.FullName);

        // Parse mode string
        const string ValidModeChars = "rwaxbt+";
        if (!mode.All(ValidModeChars.Contains) || mode.Distinct().Count() != mode.Length)
            return PyResult.ValueError(PySR.Runtime_Builtin_Open_InvalidMode, mode);

        bool reading = mode.Contains('r');
        bool writing = mode.Contains('w');
        bool appending = mode.Contains('a');
        bool creating = mode.Contains('x');
        bool updating = mode.Contains('+');
        bool binary = mode.Contains('b');

        if (new[] { creating, reading, writing, appending }.Count(v => v) > 1)
            return PyResult.ValueError(PySR.Runtime_Builtin_Open_ConflictingMode);

        // Determine file mode
        FileMode fileMode;
        FileAccess fileAccess;
        bool isSeekable = true;

        if (creating)
        {
            fileMode = FileMode.CreateNew;
            fileAccess = updating ? FileAccess.ReadWrite : FileAccess.Write;
        }
        else if (writing)
        {
            fileMode = FileMode.Create;
            fileAccess = updating ? FileAccess.ReadWrite : FileAccess.Write;
        }
        else if (appending)
        {
            // FileMode.Append only supports FileAccess.Write.
            // For a+ (append + update), use OpenOrCreate with ReadWrite and seek to end.
            fileMode = updating ? FileMode.OpenOrCreate : FileMode.Append;
            fileAccess = updating ? FileAccess.ReadWrite : FileAccess.Write;
        }
        else // reading (default)
        {
            fileMode = FileMode.Open;
            fileAccess = updating ? FileAccess.ReadWrite : FileAccess.Read;
        }

        var fs = context.PyEnvironment.FileSystem;
        var fileInfo = fs.GetFile(path);

        // Check existence for read-only or read-update without write/append/create
        bool pureRead = reading && !writing && !appending && !creating;
        if (pureRead && !fileInfo.Exists)
        {
            return PyResult.FromException(
                PyFileNotFoundErrorObjectType.Shared.Create(PyStrObject.FromString(path)));
        }

        // Check non-existence for create mode
        if (creating && fileInfo.Exists)
        {
            return PyResult.FromException(
                PyFileExistsErrorObjectType.Shared.Create(PyStrObject.FromString(path)));
        }

        Stream stream;
        try
        {
            stream = fileInfo.Open(fileMode, fileAccess, FileShare.None);
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
        {
            return PyResult.FromException(
                PyFileNotFoundErrorObjectType.Shared.Create(PyStrObject.FromString(path)));
        }
        catch (UnauthorizedAccessException)
        {
            return PyResult.RaiseException(PyPermissionErrorObjectType.Shared, path);
        }

        // For a+ mode, seek to end after opening
        if (appending && updating)
            stream.Seek(0, SeekOrigin.End);

        return new PyFileObject(stream, mode, path,
            isTextMode: !binary, isReadable: reading || updating,
            isWritable: writing || appending || updating, isSeekable);
    }
}
