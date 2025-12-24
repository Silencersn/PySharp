using PySharp.AstNodes;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using PySharp.Tokenization;
using System.Diagnostics;
using System.Text;

namespace PySharp.PyModules.Builtins;

public static partial class PyBuiltinFunctions
{
    // A
    public static readonly PyBuiltinFunctionOrMethodObject Abs = new("abs", AbsImpl);
    // TODO: aiter()
    public static readonly PyBuiltinFunctionOrMethodObject All = new("all", AllImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Any = new("any", AnyImpl);
    // TODO: ascii()

    // B
    // TODO: bin()
    // bool -> PyBoolObject
    // TODO: breakpoint()
    // TODO: bytearray()
    // TODO: bytes()

    // C
    public static readonly PyBuiltinFunctionOrMethodObject Callable = new("callable", CallableImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Chr = new("chr", ChrImpl);
    // TODO: classmethod()
    // TODO: compile()
    // TODO: complex()

    // D
    // TODO: delattr()
    // dict -> PyDictObject
    public static readonly PyBuiltinFunctionOrMethodObject Dir = new("dir", DirImpl_1, DirImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject DivMod = new("divmod", DivModImpl);

    // E
    // TODO: enumerate()
    public static readonly PyBuiltinFunctionOrMethodObject Eval = new("eval", EvalImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Exec = new("exec", ExecImpl);

    // F
    // TODO: filter
    // float -> PyFloatObject
    // TODO: format()
    // TODO: frozenset()

    // G
    public static readonly PyBuiltinFunctionOrMethodObject GetAttr = new("getattr", GetAttrImpl_1, GetAttrImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject Globals = new("globals", GlobalsImpl);

    // H
    public static readonly PyBuiltinFunctionOrMethodObject HasAttr = new("hasattr", HasAttrImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Hash = new("hash", HashImpl);
    // TODO: help()
    // TODO: hex()

    // I
    public static readonly PyBuiltinFunctionOrMethodObject Id = new("id", IdImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Input = new("input", InputImpl_1, InputImpl_2);
    // int -> PyIntObject
    public static readonly PyBuiltinFunctionOrMethodObject IsInstance = new("isinstance", IsInstanceImpl);
    public static readonly PyBuiltinFunctionOrMethodObject IsSubclass = new("issubclass", IsSubclassImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Iter = new("iter", IterImpl);

    // L
    public static readonly PyBuiltinFunctionOrMethodObject Len = new("len", LenImpl);
    // list -> PyListObject
    public static readonly PyBuiltinFunctionOrMethodObject Locals = new("locals", LocalsImpl);

    // M
    // map -> PyMapObject
    public static readonly PyBuiltinFunctionOrMethodObject Max = new("max", MaxImpl_1, MaxImpl_2, MaxImpl_3);
    // TODO: memoryview()
    public static readonly PyBuiltinFunctionOrMethodObject Min = new("min", MinImpl_1, MinImpl_2, MinImpl_3);

    // N
    public static readonly PyBuiltinFunctionOrMethodObject Next = new("next", NextImpl_1, NextImpl_2);

    // O
    // object -> PyObject
    // TODO: oct()
    // TODO: open()
    public static readonly PyBuiltinFunctionOrMethodObject Ord = new("ord", OrdImpl);

    // P
    public static readonly PyBuiltinFunctionOrMethodObject Pow = new("pow", PowImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Print = new("print", PrintImpl);
    // property -> PyPropertyObject

    // R
    // range -> PyRangeObject
    public static readonly PyBuiltinFunctionOrMethodObject Repr = new("repr", ReprImpl);
    // TODO: reversed()
    // TODO: round()

    // S
    // set -> PySetObject
    public static readonly PyBuiltinFunctionOrMethodObject SetAttr = new("setattr", SetAttrImpl);
    // slice -> PySliceObject
    // TODO: sorted()
    // TODO: staticmethod()
    // str -> PyStrObject
    public static readonly PyBuiltinFunctionOrMethodObject Sum = new("sum", SumImpl);
    // super -> PySuperObject

    // T
    // tuple -> PyTupleObject
    // type -> PyTypeObject

    // V
    // TODO: vars()

    // Z
    // zip -> PyZipObject

    // _
    public static readonly PyBuiltinFunctionOrMethodObject Import = new(PySpecialNames.Import, ImportImpl);


    [PyFunctionArgsDef("*objects", "sep=' '", "end='\\n'", "file=None", "flush=False")]
    private static PyResult PrintImpl(PyCallContext context, PyArguments arguments)
    {
        var sepObj = arguments.Kwargs["sep"];
        if (!Utils.TryGetValue(sepObj, (PyStrObject str) => str.Value, " ", out var sep))
            return PyResult.RaiseTypeError($"end must be None or a string, not {sepObj.PyType.Name}");

        var endObj = arguments.Kwargs["end"];
        if (!Utils.TryGetValue(endObj, (PyStrObject str) => str.Value, "\n", out var end))
            return PyResult.RaiseTypeError($"end must be None or a string, not {endObj.PyType.Name}");

        if (!PySpecialMethods.TryGetBool(context, arguments.Kwargs["flush"], out var flushObj, out var result))
            return result;

        for (int i = 0; i < arguments.ExtraArgs.Count; i++)
        {
            if (i is not 0)
                context.Out.Write(sep);

            if (!PySpecialMethods.TryGetStr(context, arguments.ExtraArgs[i], out var str, out result))
                return result;
            context.Out.Write(str.Value);
        }
        context.Out.Write(end);
        if (flushObj.BoolValue)
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

        if (result.IsNotImplemented)
            return PyResult.RaiseTypeError($"unsupported operand type(s) for ** or pow(): '{baseObj.PyType.Name}', '{expObj.PyType.Name}', 'int'");

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
        if (!PySpecialMethods.TryGetStr(context, arguments.Args[0], out var s, out var result))
            return result;
        context.Out.Write(s.Value);
        var str = PyStrObject.FromString(context.In.ReadLine() ?? string.Empty);
        return str;
    }
    [PyFunctionArgsDef("source", "/", "globals=None", "locals=None")]
    private static PyResult EvalImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments.Args[0] is not PyStrObject str)
            return PyResult.RaiseTypeError(null);
        var parser = new Parser("<string>", PyVirtualMachine.PyEnvironment.OptimizationOptions, Lexer.Tokenize(str.Value));
        var node = parser.ParseExpressionNode();
        var frame = context.CurrentFrame;
        var tempFrame = frame.TempFrame(FrameType.Eval);

        try
        {
            return node.Body.GetExprValue(context, tempFrame);
        }
        catch (PyRuntimeException e)
        {
            return PyResult.FromException(e.PyException);
        }
    }
    [PyFunctionArgsDef("source", "/", "globals=None", "locals=None", "*", "closure=None")]
    private static PyResult ExecImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments.Args[0] is not PyStrObject str)
            return PyResult.RaiseTypeError(null);
        ModuleNode node;
        try
        {
            var tokens = Lexer.Tokenize(str.Value);
            node = Parser.Parse("<string>", tokens, PyVirtualMachine.PyEnvironment);
        }
        catch (TokenizationException e)
        {
            return PyResult.RaiseSyntaxError(e.Message);
        }
        catch (AstException e)
        {
            return PyResult.RaiseSyntaxError(e.Message);
        }
        var frame = context.CurrentFrame;
        var tempFrame = frame.TempFrame(FrameType.Exec);
        node.Execute(context, tempFrame);
        return PyNoneObject.None;
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
            if (!PySpecialMethods.TryGetBool(context, element.Value, out var value, out var result))
                return result;
            if (!value.BoolValue)
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
            if (!PySpecialMethods.TryGetBool(context, element.Value, out var value, out var result))
                return result;
            if (value.BoolValue)
                return PyBoolObject.True;
        }
        return PyBoolObject.False;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "key=None")]
    private static PyResult MaxImpl_1(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            return PyResult.RaiseTypeError("max() with key not implemented");
        if (!Utils.TryEnumeratedIterable(context, iterable, out var elements, out var err))
            return err.Value;
        PyObject? result = null;
        foreach (var element in elements)
        {
            if (result is null)
            {
                result = element;
                continue;
            }
            var gt = PyOperators.Gt(context, element, result);
            if (gt.IsError)
                return gt;
            if (!PySpecialMethods.TryGetBool(context, gt.Value, out var b, out var bResult))
                return bResult;
            if (b.BoolValue)
                result = element;
        }
        if (result is null)
            return PyResult.RaiseValueError("max() iterable argument is empty");
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "default", "key=None")]
    private static PyResult MaxImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();
        if (!Utils.TryEnumeratedIterable(context, iterable, out var elements, out var err))
            return err.Value;
        PyObject result = arguments["default"];
        foreach (var element in elements)
        {
            var gt = PyOperators.Gt(context, element, result);
            if (gt.IsError)
                return gt;
            if (!PySpecialMethods.TryGetBool(context, gt.Value, out var b, out var bResult))
                return bResult;
            if (b.BoolValue)
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
            if (!PySpecialMethods.TryGetBool(context, gt.Value, out var b, out var bResult))
                return bResult;
            if (b.BoolValue)
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
        if (!Utils.TryEnumeratedIterable(context, iterable, out var elements, out var err))
            return err.Value;
        PyObject? result = null;
        foreach (var element in elements)
        {
            if (result is null)
            {
                result = element;
                continue;
            }
            var lt = PyOperators.Lt(context, element, result);
            if (lt.IsError)
                return lt;
            if (!PySpecialMethods.TryGetBool(context, lt.Value, out var b, out var bResult))
                return bResult;
            if (b.BoolValue)
                result = element;
        }
        if (result is null)
            return PyResult.RaiseValueError("min() iterable argument is empty");
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "default", "key=None")]
    private static PyResult MinImpl_2(PyCallContext context, PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();
        if (!Utils.TryEnumeratedIterable(context, iterable, out var elements, out var err))
            return err.Value;
        PyObject result = arguments["default"];
        foreach (var element in elements)
        {
            var lt = PyOperators.Lt(context, element, result);
            if (lt.IsError)
                return lt;
            if (!PySpecialMethods.TryGetBool(context, lt.Value, out var b, out var bResult))
                return bResult;
            if (b.BoolValue)
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
            if (!PySpecialMethods.TryGetBool(context, lt.Value, out var b, out var bResult))
                return bResult;
            if (b.BoolValue)
                result = element;
        }
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "start=0")]
    private static PyResult SumImpl(PyCallContext context, PyArguments arguments)
    {
        var start = arguments["start"];
        if (start is PyStrObject)
            return PyResult.RaiseTypeError("sum() can't sum strings [use ''.join(seq) instead]");
        if (!Utils.TryEnumeratedIterable(context, arguments[0], out var iterable, out var err))
            return err.Value;
        var result = start;
        foreach (var item in iterable)
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
        var obj = arguments[0];
        if (!Utils.TryCastStrAsArg(arguments[1], out var name, out var err, "attribute name"))
            return err.Value;
        return PyOperators.GetAttr(context, obj, name);
    }

    [PyFunctionArgsDef("object", "name", "default", "/")]
    private static PyResult GetAttrImpl_2(PyCallContext context, PyArguments arguments)
    {
        var obj = arguments[0];
        if (!Utils.TryCastStrAsArg(arguments[1], out var name, out var err, "attribute name"))
            return err.Value;
        var attr = PyOperators.GetAttr(context, obj, name);
        if (!attr.IsAttributeError)
            return attr;
        return arguments[2];
    }

    [PyFunctionArgsDef("object", "name", "value", "/")]
    private static PyResult SetAttrImpl(PyCallContext context, PyArguments arguments)
    {
        if (!Utils.TryCastStrAsArg(arguments[1], out var name, out var err, "attribute name"))
            return err.Value;
        return PyOperators.SetAttr(context, arguments[0], name, arguments[2]);
    }

    [PyFunctionArgsDef("object", "name", "/")]
    private static PyResult HasAttrImpl(PyCallContext context, PyArguments arguments)
    {
        if (!Utils.TryCastStrAsArg(arguments[1], out var name, out var err, "attribute name"))
            return err.Value;
        var attr = PyOperators.GetAttr(context, arguments[0], name);
        if (attr.IsSuccessful)
            return PyBoolObject.True;
        if (attr.IsAttributeError)
            return PyBoolObject.False;
        return attr;
    }

    [PyFunctionArgsDef()]
    private static PyResult DirImpl_1(PyCallContext context, PyArguments arguments)
    {
        var result = PyListObject.CreateList(context.CurrentFrame.Locals
            .Concat(context.CurrentFrame.Closures.Select(static pair => KeyValuePair.Create(pair.Key, pair.Value.Value)))
            .Where(static pair => pair.Value is not null)
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
        if (!PySpecialMethods.TryGetIndex(context, arguments[0], out var value, out var result))
            return result;
        if (!Rune.TryCreate(value.Int32Value, out var rune))
            return PyResult.RaiseValueError("chr() arg not in range(0x110000)");
        return PyStrObject.FromRune(rune);
    }

    [PyFunctionArgsDef("c", "/")]
    private static PyResult OrdImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyResult.RaiseTypeError($"ord() expected string of length 1, but {arguments[0].PyType.Name} found");
        if (strObj.PyLength is not 1)
            return PyResult.RaiseTypeError($"ord() expected a character, but string of length {strObj.PyLength} found");
        var successful = Rune.TryGetRuneAt(strObj.Value, 0, out var rune);
        Debug.Assert(successful);
        return PyIntObject.FromInteger(rune.Value);
    }

    [PyFunctionArgsDef()]
    private static PyResult LocalsImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyDictObject.CreateDict(context.CurrentFrame.Locals
            .Concat(context.CurrentFrame.Closures.Select(static pair => KeyValuePair.Create(pair.Key, pair.Value.Value)))
            .Where(static pair => pair.Value is not null)
            .Select(static pair => KeyValuePair.Create((PyObject)PyStrObject.FromString(pair.Key), pair.Value!)));
        return result;
    }

    [PyFunctionArgsDef()]
    private static PyResult GlobalsImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyDictObject.CreateProxy(context.CurrentFrame.GlobalsAdapter);
        return result;
    }

    [PyFunctionArgsDef("name", "globals=None", "locals=None", "fromlist=()", "level=0")]
    private static PyResult ImportImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyResult.RaiseTypeError("module name must be a string");
        var name = strObj.Value;
        if (!PyVirtualMachine.PyEnvironment.TryLoadModule(context, name, out var module))
            return PyResult.RaiseException(PyStandardExceptionTypes.ModuleNotFoundError, $"No module named '{name}'");
        return module;
    }

    [PyFunctionArgsDef("object", "classinfo", "/")]
    private static PyResult IsInstanceImpl(PyCallContext context, PyArguments arguments)
    {
        var ret = IsInstanceForUnknown(arguments[0], arguments[1]);
        if (ret is null)
            return PyResult.RaiseTypeError("isinstance() arg 2 must be a type or a tuple of types");
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
            return PyResult.RaiseTypeError("issubclass() arg 1 must be a class");
        var ret = IsSubclassForUnknown(typeObj, arguments[1]);
        if (ret is null)
            return PyResult.RaiseTypeError("issubclass() arg 2 must be a type or a tuple of types");
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
        return PySpecialMethods.GetHash(context, arguments[0]);
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyResult IterImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Iter(context, arguments[0]);
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyResult LenImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.GetLen(context, arguments[0]);
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
        return PySpecialMethods.GetRepr(context, arguments[0]);
    }


    [PyFunctionArgsDef("x", "/")]
    private static PyResult AbsImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Abs(context, arguments[0]);
    }
}