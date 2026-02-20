using PySharp.Compilation.Bytecodes;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;
using System.Diagnostics;
using System.Text;

namespace PySharp.Modules.Builtins;

public static partial class PyBuiltinFunctions
{
    // A
    public static readonly PyBuiltinFunctionOrMethodObject Abs = PyBuiltinFunctionOrMethodObject.CreateFunction("abs", AbsImpl);
    // TODO: aiter()
    public static readonly PyBuiltinFunctionOrMethodObject All = PyBuiltinFunctionOrMethodObject.CreateFunction("all", AllImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Any = PyBuiltinFunctionOrMethodObject.CreateFunction("any", AnyImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Ascii = PyBuiltinFunctionOrMethodObject.CreateFunction("ascii", AsciiImpl);

    // B
    public static readonly PyBuiltinFunctionOrMethodObject Bin = PyBuiltinFunctionOrMethodObject.CreateFunction("bin", BinImpl);
    // bool -> PyBoolObject
    // TODO: breakpoint()
    // TODO: bytearray()
    // TODO: bytes()

    // C
    public static readonly PyBuiltinFunctionOrMethodObject Callable = PyBuiltinFunctionOrMethodObject.CreateFunction("callable", CallableImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Chr = PyBuiltinFunctionOrMethodObject.CreateFunction("chr", ChrImpl);
    // classmethod -> PyClassMethodObject
    public static readonly PyBuiltinFunctionOrMethodObject Compile = PyBuiltinFunctionOrMethodObject.CreateFunction("compile", CompileImpl);
    // complex -> PyComplexObject

    // D
    public static readonly PyBuiltinFunctionOrMethodObject DelAttr = PyBuiltinFunctionOrMethodObject.CreateFunction("delattr", DelAttrImpl);
    // dict -> PyDictObject
    public static readonly PyBuiltinFunctionOrMethodObject Dir = PyBuiltinFunctionOrMethodObject.CreateFunction("dir", DirImpl_1, DirImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject DivMod = PyBuiltinFunctionOrMethodObject.CreateFunction("divmod", DivModImpl);

    // E
    // TODO: enumerate()
    public static readonly PyBuiltinFunctionOrMethodObject Eval = PyBuiltinFunctionOrMethodObject.CreateFunction("eval", EvalImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Exec = PyBuiltinFunctionOrMethodObject.CreateFunction("exec", ExecImpl);

    // F
    // TODO: filter
    // float -> PyFloatObject
    public static readonly PyBuiltinFunctionOrMethodObject Format = PyBuiltinFunctionOrMethodObject.CreateFunction("format", FormatImpl);
    // TODO: frozenset()

    // G
    public static readonly PyBuiltinFunctionOrMethodObject GetAttr = PyBuiltinFunctionOrMethodObject.CreateFunction("getattr", GetAttrImpl_1, GetAttrImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject Globals = PyBuiltinFunctionOrMethodObject.CreateFunction("globals", GlobalsImpl);

    // H
    public static readonly PyBuiltinFunctionOrMethodObject HasAttr = PyBuiltinFunctionOrMethodObject.CreateFunction("hasattr", HasAttrImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Hash = PyBuiltinFunctionOrMethodObject.CreateFunction("hash", HashImpl);
    // TODO: help()
    public static readonly PyBuiltinFunctionOrMethodObject Hex = PyBuiltinFunctionOrMethodObject.CreateFunction("hex", HexImpl);

    // I
    public static readonly PyBuiltinFunctionOrMethodObject Id = PyBuiltinFunctionOrMethodObject.CreateFunction("id", IdImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Input = PyBuiltinFunctionOrMethodObject.CreateFunction("input", InputImpl_1, InputImpl_2);
    // int -> PyIntObject
    public static readonly PyBuiltinFunctionOrMethodObject IsInstance = PyBuiltinFunctionOrMethodObject.CreateFunction("isinstance", IsInstanceImpl);
    public static readonly PyBuiltinFunctionOrMethodObject IsSubclass = PyBuiltinFunctionOrMethodObject.CreateFunction("issubclass", IsSubclassImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Iter = PyBuiltinFunctionOrMethodObject.CreateFunction("iter", IterImpl);

    // L
    public static readonly PyBuiltinFunctionOrMethodObject Len = PyBuiltinFunctionOrMethodObject.CreateFunction("len", LenImpl);
    // list -> PyListObject
    public static readonly PyBuiltinFunctionOrMethodObject Locals = PyBuiltinFunctionOrMethodObject.CreateFunction("locals", LocalsImpl);

    // M
    // map -> PyMapObject
    public static readonly PyBuiltinFunctionOrMethodObject Max = PyBuiltinFunctionOrMethodObject.CreateFunction("max", MaxImpl_1, MaxImpl_2, MaxImpl_3);
    // TODO: memoryview()
    public static readonly PyBuiltinFunctionOrMethodObject Min = PyBuiltinFunctionOrMethodObject.CreateFunction("min", MinImpl_1, MinImpl_2, MinImpl_3);

    // N
    public static readonly PyBuiltinFunctionOrMethodObject Next = PyBuiltinFunctionOrMethodObject.CreateFunction("next", NextImpl_1, NextImpl_2);

    // O
    // object -> PyObject
    public static readonly PyBuiltinFunctionOrMethodObject Oct = PyBuiltinFunctionOrMethodObject.CreateFunction("oct", OctImpl);
    // TODO: open()
    public static readonly PyBuiltinFunctionOrMethodObject Ord = PyBuiltinFunctionOrMethodObject.CreateFunction("ord", OrdImpl);

    // P
    public static readonly PyBuiltinFunctionOrMethodObject Pow = PyBuiltinFunctionOrMethodObject.CreateFunction("pow", PowImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Print = PyBuiltinFunctionOrMethodObject.CreateFunction("print", PrintImpl);
    // property -> PyPropertyObject

    // R
    // range -> PyRangeObject
    public static readonly PyBuiltinFunctionOrMethodObject Repr = PyBuiltinFunctionOrMethodObject.CreateFunction("repr", ReprImpl);
    // TODO: reversed()
    // TODO: round()

    // S
    // set -> PySetObject
    public static readonly PyBuiltinFunctionOrMethodObject SetAttr = PyBuiltinFunctionOrMethodObject.CreateFunction("setattr", SetAttrImpl);
    // slice -> PySliceObject
    // TODO: sorted()
    // staticmethod -> PyStaticMethodObject
    // str -> PyStrObject
    public static readonly PyBuiltinFunctionOrMethodObject Sum = PyBuiltinFunctionOrMethodObject.CreateFunction("sum", SumImpl);
    // super -> PySuperObject

    // T
    // tuple -> PyTupleObject
    // type -> PyTypeObject

    // V
    // TODO: vars()

    // Z
    // zip -> PyZipObject

    // _
    public static readonly PyBuiltinFunctionOrMethodObject Import = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.Import, ImportImpl);


    [PyFunctionArgsDef("*objects", "sep=' '", "end='\\n'", "file=None", "flush=False")]
    private static PyResult PrintImpl(PyCallContext context, PyArguments arguments)
    {
        var sepObj = arguments.Kwargs["sep"];
        if (!Utils.TryGetValue(sepObj, (PyStrObject str) => str.Value, " ", out var sep))
            return PyResult.TypeError(PySR.Runtime_Builtin_Print_WrongArgType, "sep", sepObj.PyType.FullName);

        var endObj = arguments.Kwargs["end"];
        if (!Utils.TryGetValue(endObj, (PyStrObject str) => str.Value, "\n", out var end))
            return PyResult.TypeError(PySR.Runtime_Builtin_Print_WrongArgType, "end", endObj.PyType.FullName);

        var result = PySpecialMethods.Bool(context, arguments.Kwargs["flush"]);
        if (result.IsError)
            return result;

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
        if (result.Value.BoolValue)
            context.Out.Flush();

        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("base", "exp", "mod=None")]
    private static PyResult PowImpl(PyCallContext context, PyArguments arguments)
    {
        var baseObj = arguments.Args[0];
        var expObj = arguments.Args[1];
        var modObj = arguments.Args[2];

        var result = PyOperators.Pow(context, baseObj, expObj, modObj);
        if (result.IsError)
            return result;

        Debug.Assert(!result.IsNotImplemented);
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult DivModImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.DivMod(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef()]
    private static PyResult InputImpl_1(PyCallContext context, PyArguments arguments)
    {
        var str = PyStrObject.FromString(context.In.ReadLine() ?? string.Empty);
        return str;
    }
    [PyFunctionArgsDef("prompt", "/")]
    private static PyResult InputImpl_2(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Str(context, arguments[0]);
        if (result.IsError)
            return result;

        context.Out.Write(result.Value.Value);
        var str = PyStrObject.FromString(context.In.ReadLine() ?? string.Empty);
        return str;
    }
    [PyFunctionArgsDef("source", "/", "globals=None", "locals=None")]
    private static PyResult EvalImpl(PyCallContext context, PyArguments arguments)
    {
        var source = arguments[0];
        if (source is not PyStrObject && source is not PyCodeObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Arg1WrongType, "eval");

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

        var frame = context.CurrentFrame;

        if (source is PyCodeObject code)
        {
            var newFrame = frame.CreateExecEvalFrame(FrameType.Eval, globalsDict, localsDict);
            using var withFrame = context.WithFrame(newFrame);
            return PyCore.Eval(context, code.Bytecode);
        }

        Debug.Assert(source is PyStrObject);

        try
        {
            var newFrame = frame.CreateExecEvalFrame(FrameType.Eval, globalsDict, localsDict);
            using var withFrame = context.WithFrame(newFrame);
            var codeObj = Compiler.CompileEval(context, ((PyStrObject)source).Value, "<string>", onlyAsName: true);
            return PyCore.Eval(context, codeObj.Bytecode);
        }
        catch (PyRuntimeException e)
        {
            e.PyException.WithTraceback(context, overwriteExisting: false);
            context.EnsureFrameState(frame);

            return PyResult.FromException(e.PyException);
        }
    }
    [PyFunctionArgsDef("source", "/", "globals=None", "locals=None", "*", "closure=None")]
    private static PyResult ExecImpl(PyCallContext context, PyArguments arguments)
    {
        var source = arguments[0];
        if (source is not PyStrObject && source is not PyCodeObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Arg1WrongType, "exec");

        var globals = arguments[1];
        var globalsDict = globals as PyDictObject;
        if (globalsDict is null && globals is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Globals);

        var locals = arguments[2];
        var localsDict = locals as PyDictObject;
        if (localsDict is null && locals is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_ExecEval_Locals);

        var closure = arguments["closure"];
        if (closure is not PyNoneObject && source is not PyCodeObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_Exec_ClosureForNonCodeObj);

        var frame = context.CurrentFrame;

        if (source is PyCodeObject code)
        {
            if (closure is not PyNoneObject && code.FreeVars.Length is 0)
                return PyResult.TypeError(PySR.Runtime_Builtin_Exec_CannotUseClosure);

            var closureTuple = closure as PyTupleObject;
            if (!(closure is PyNoneObject ||
                closureTuple is not null &&
                closureTuple._array.Length == code.FreeVars.Length &&
                closureTuple._array.All(static obj => obj is PyCellObject)))
            {
                return PyResult.TypeError(PySR.Runtime_Builtin_Exec_WrongClosure, code.FreeVars.Length);
            }

            Debug.Assert(code.Bytecode is not null);
            var newFrame = frame.CreateExecEvalFrame(FrameType.Exec, globalsDict, localsDict, closureTuple, code);
            using var withFrame = context.WithFrame(newFrame);
            return PyCore.Eval(context, code.Bytecode);
        }

        Debug.Assert(closure is PyNoneObject);
        Debug.Assert(source is PyStrObject);

        try
        {
            var newFrame = frame.CreateExecEvalFrame(FrameType.Exec, globalsDict, localsDict);
            using var withFrame = context.WithFrame(newFrame);
            var codeObj = Compiler.CompileExec(context, ((PyStrObject)source).Value, "<string>", onlyAsName: true);
            return PyCore.Eval(context, codeObj.Bytecode);
        }
        catch (PyRuntimeException e)
        {
            e.PyException.WithTraceback(context, overwriteExisting: false);
            context.EnsureFrameState(frame);

            return PyResult.FromException(e.PyException);
        }
    }

    [PyFunctionArgsDef("iterable")]
    private static PyResult AllImpl(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments.Args[0];
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

    [PyFunctionArgsDef("iterable")]
    private static PyResult AnyImpl(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments.Args[0];
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

    [PyFunctionArgsDef("iterable", "/", "*", "key=None")]
    private static PyResult MaxImpl_1(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            return PyResult.RaisePySharpException("max() with key not implemented");

        var elements = PyUtils.IterableToList(context, iterable);
        if (elements.IsError)
            return elements;

        PyObject? result = null;
        foreach (var element in elements.Value._list)
        {
            if (result is null)
            {
                result = element;
                continue;
            }
            var gt = PyOperators.Gt(context, element, result);
            if (gt.IsError)
                return gt;
            var bResult = PySpecialMethods.Bool(context, gt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
                result = element;
        }
        if (result is null)
            return PyResult.ValueError(PySR.Runtime_Builtin_Max_EmptyIterable);
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "default", "key=None")]
    private static PyResult MaxImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        var elements = PyUtils.IterableToList(context, iterable);
        if (elements.IsError)
            return elements;

        PyObject result = arguments["default"];
        foreach (var element in elements.Value._list)
        {
            var gt = PyOperators.Gt(context, element, result);
            if (gt.IsError)
                return gt;
            var bResult = PySpecialMethods.Bool(context, gt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
                result = element;
        }
        return result;
    }

    [PyFunctionArgsDef("arg1", "arg2", "/", "*args", "key=None")]
    private static PyResult MaxImpl_3(PyCallContext context, PyArguments arguments)
    {
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();
        PyObject result = arguments[0];
        foreach (var element in arguments.ExtraArgs.Prepend(arguments[1]))
        {
            var gt = PyOperators.Gt(context, element, result);
            if (gt.IsError)
                return gt;
            var bResult = PySpecialMethods.Bool(context, gt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
                result = element;
        }
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "key=None")]
    private static PyResult MinImpl_1(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        var elements = PyUtils.IterableToList(context, iterable);
        if (elements.IsError)
            return elements;

        PyObject? result = null;
        foreach (var element in elements.Value._list)
        {
            if (result is null)
            {
                result = element;
                continue;
            }
            var lt = PyOperators.Lt(context, element, result);
            if (lt.IsError)
                return lt;
            var bResult = PySpecialMethods.Bool(context, lt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
                result = element;
        }
        if (result is null)
            return PyResult.ValueError(PySR.Runtime_Builtin_Min_EmptyIterable);
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "default", "key=None")]
    private static PyResult MinImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        var elements = PyUtils.IterableToList(context, iterable);
        if (elements.IsError)
            return elements;

        PyObject result = arguments["default"];
        foreach (var element in elements.Value._list)
        {
            var lt = PyOperators.Lt(context, element, result);
            if (lt.IsError)
                return lt;
            var bResult = PySpecialMethods.Bool(context, lt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
                result = element;
        }
        return result;
    }

    [PyFunctionArgsDef("arg1", "arg2", "/", "*args", "key=None")]
    private static PyResult MinImpl_3(PyCallContext context, PyArguments arguments)
    {
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();
        PyObject result = arguments[0];
        foreach (var element in arguments.ExtraArgs.Prepend(arguments[1]))
        {
            var lt = PyOperators.Lt(context, element, result);
            if (lt.IsError)
                return lt;
            var bResult = PySpecialMethods.Bool(context, lt.Value);
            if (bResult.IsError)
                return bResult;
            if (bResult.Value.BoolValue)
                result = element;
        }
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "start=0")]
    private static PyResult SumImpl(PyCallContext context, PyArguments arguments)
    {
        var start = arguments[1];
        if (start is PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Builtin_Sum_Strings);

        var list = PyUtils.IterableToList(context, arguments[0]);
        if (list.IsError)
            return list;

        var result = start;
        foreach (var item in list.Value._list)
        {
            var ret = PyOperators.Add(context, result, item);
            if (ret.IsError)
                return ret;
            result = ret.Value;
        }
        return result;
    }

    [PyFunctionArgsDef("object", "name", "/")]
    private static PyResult GetAttrImpl_1(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.GetAttr(context, arguments[0], arguments[1]);
    }

    [PyFunctionArgsDef("object", "name", "default", "/")]
    private static PyResult GetAttrImpl_2(PyCallContext context, PyArguments arguments)
    {
        var attr = PyOperators.GetAttr(context, arguments[0], arguments[1]);
        if (!attr.IsAttributeError)
            return attr;
        return arguments[2];
    }

    [PyFunctionArgsDef("object", "name", "value", "/")]
    private static PyResult SetAttrImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.SetAttr(context, arguments[0], arguments[1], arguments[2]);
    }

    [PyFunctionArgsDef("object", "name", "/")]
    private static PyResult HasAttrImpl(PyCallContext context, PyArguments arguments)
    {
        var attr = PyOperators.GetAttr(context, arguments[0], arguments[1]);
        if (attr.IsSuccessful)
            return PyBoolObject.True;
        if (attr.IsAttributeError)
            return PyBoolObject.False;
        return attr;
    }

    [PyFunctionArgsDef()]
    private static PyResult DirImpl_1(PyCallContext context, PyArguments arguments)
    {
        var result = PyListObject.CreateList(context.CurrentFrame.Variables
            .EnumerateLocals()
            .Select(static pair => PyStrObject.FromString(pair.Key)));
        return result;
    }
    [PyFunctionArgsDef("object", "/")]
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

    [PyFunctionArgsDef("codepoint", "/")]
    private static PyResult ChrImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        if (!Rune.TryCreate(result.Value.Int32Value, out var rune))
            return PyResult.ValueError(PySR.Runtime_Builtin_Chr_OutOfRange);
        return PyStrObject.FromRune(rune);
    }

    [PyFunctionArgsDef("c", "/")]
    private static PyResult OrdImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyResult.TypeError(PySR.Runtime_Builtin_Ord_ExpectedString, arguments[0].PyType.Name);
        if (strObj.PyLength is not 1)
            return PyResult.TypeError(PySR.Runtime_Builtin_Ord_ExpectedACharacter, strObj.PyLength);
        return PyIntObject.FromInteger(strObj.PyCharAt(0).Value);
    }

    [PyFunctionArgsDef()]
    private static PyResult LocalsImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyDictObject.CreateDict(context.CurrentFrame.Variables
            .EnumerateLocals()
            .Select(static pair => KeyValuePair.Create((PyObject)PyStrObject.FromString(pair.Key), pair.Value)));
        return result;
    }

    [PyFunctionArgsDef()]
    private static PyResult GlobalsImpl(PyCallContext context, PyArguments arguments)
    {
        var result = context.CurrentFrame.Variables._globals.PyDict;
        return result;
    }

    [PyFunctionArgsDef("name", "globals=None", "locals=None", "fromlist=()", "level=0")]
    private static PyResult ImportImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyResult.TypeError(PySR.Runtime_Builtin_Import_NameMustBeString);
        var name = strObj.Value;
        if (!context.PyEnvironment.TryLoadModule(context, name, out var module))
            return PyResult.ModuleNotFoundError(PySR.Runtime_Import_ModuleNotFound, name);
        return module;
    }

    [PyFunctionArgsDef("object", "classinfo", "/")]
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
            foreach (var type in types._array)
            {
                var ret = IsInstanceForUnknown(obj, type);
                if (ret is null or true)
                    return ret;
            }
            return false;
        }
    }

    [PyFunctionArgsDef("class", "classinfo", "/")]
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
            foreach (var type in types._array)
            {
                var ret = IsSubclassForUnknown(obj, type);
                if (ret is null or true)
                    return ret;
            }
            return false;
        }
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyResult CallableImpl(PyCallContext context, PyArguments arguments)
    {
        var attr = PyOperators.GetAttr(context, arguments[0], PySpecialNames.Call);
        if (attr.IsSuccessful)
            return PyBoolObject.True;
        if (attr.IsAttributeError)
            return PyBoolObject.False;
        return attr;
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyResult IdImpl(PyCallContext context, PyArguments arguments)
    {
        return PyIntObject.FromInteger(arguments[0].PyId);
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyResult HashImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Hash(context, arguments[0]);
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyResult IterImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Iter(context, arguments[0]);
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyResult LenImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Len(context, arguments[0]);
    }

    [PyFunctionArgsDef("iterator", "/")]
    private static PyResult NextImpl_1(PyCallContext context, PyArguments arguments)
    {
        var iterator = arguments[0];
        return PySpecialMethods.Next(context, iterator);
    }

    [PyFunctionArgsDef("iterator", "default", "/")]
    private static PyResult NextImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterator = arguments[0];
        var result = PySpecialMethods.Next(context, iterator);
        if (result.IsStopIteration)
            return arguments[1];
        return result;
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyResult ReprImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Repr(context, arguments[0]);
    }


    [PyFunctionArgsDef("x", "/")]
    private static PyResult AbsImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Abs(context, arguments[0]);
    }

    [PyFunctionArgsDef("integer", "/")]
    private static PyResult BinImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;

        var value = BigIntegerHelper.ToString(result.Value.Value, 2);
        return PyStrObject.FromString(value);
    }

    [PyFunctionArgsDef("integer", "/")]
    private static PyResult OctImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;

        var value = BigIntegerHelper.ToString(result.Value.Value, 8);
        return PyStrObject.FromString(value);
    }

    [PyFunctionArgsDef("integer", "/")]
    private static PyResult HexImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;

        var value = BigIntegerHelper.ToString(result.Value.Value, 16);
        return PyStrObject.FromString(value);
    }

    [PyFunctionArgsDef("object", "/")]
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

    [PyFunctionArgsDef("value", "format_spec=''", "/")]
    private static PyResult FormatImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Format(context, arguments[0], arguments[1]);
    }

    [PyFunctionArgsDef("object", "name", "/")]
    private static PyResult DelAttrImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.DelAttr(context, arguments[0], arguments[1]);
    }

    [PyFunctionArgsDef("source", "filename", "mode" /* flags=0, dont_inherit=False, optimize=-1 */)]
    private static PyResult CompileImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject source)
            // TODO: bytes, ast
            return PyResult.TypeError(PySR.Runtime_Builtin_Compile_Arg1WrongType);

        if (arguments[1] is not PyStrObject filename)
            return PyResult.TypeError(PySR.Runtime_Builtin_Compile_FilenameWrongType, arguments[1].PyType.FullName);

        if (arguments[2] is not PyStrObject mode)
            return PyResult.TypeError(PySR.Runtime_Builtin_Compile_ModeWrongType, arguments[2].PyType.FullName);

        var bytecode = mode.Value switch
        {
            "exec" => Compiler.CompileExec(context, source.Value, filename.Value, onlyAsName: true).Bytecode,
            "eval" => Compiler.CompileEval(context, source.Value, filename.Value, onlyAsName: true).Bytecode,
            "single" => Compiler.CompileSingle(context, source.Value, filename.Value, appendNewLine: false, onlyAsName: true).Bytecode,
            _ => null
        };

        if (bytecode is null)
            return PyResult.TypeError(PySR.Runtime_Builtin_Compile_WrongMode);

        return new PyCodeObject(filename.Value, bytecode);
    }
}