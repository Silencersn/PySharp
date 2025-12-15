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
    public static readonly PyBuiltinFunctionOrMethodObject Abs = new("abs", PySpecialMethods.Abs);
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
    public static readonly PyBuiltinFunctionOrMethodObject Hash = new("hash", PySpecialMethods.GetHash);
    // TODO: help()
    // TODO: hex()

    // I
    public static readonly PyBuiltinFunctionOrMethodObject Id = new("id", IdImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Input = new("input", InputImpl_1, InputImpl_2);
    // int -> PyIntObject
    public static readonly PyBuiltinFunctionOrMethodObject IsInstance = new("isinstance", IsInstanceImpl);
    public static readonly PyBuiltinFunctionOrMethodObject IsSubclass = new("issubclass", IsSubclassImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Iter = new("iter", PySpecialMethods.Iter);

    // L
    public static readonly PyBuiltinFunctionOrMethodObject Len = new("len", PySpecialMethods.GetLen);
    // list -> PyListObject
    public static readonly PyBuiltinFunctionOrMethodObject Locals = new("locals", LocalsImpl);

    // M
    // TODO: map()
    public static readonly PyBuiltinFunctionOrMethodObject Max = new("max", MaxImpl_1, MaxImpl_2, MaxImpl_3);
    // TODO: memoryview()
    public static readonly PyBuiltinFunctionOrMethodObject Min = new("min", MinImpl_1, MinImpl_2, MinImpl_3);

    // N
    public static readonly PyBuiltinFunctionOrMethodObject Next = new("next", PySpecialMethods.Next);

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
    public static readonly PyBuiltinFunctionOrMethodObject Repr = new("repr", PySpecialMethods.GetRepr);
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
    private static PyObject? PrintImpl(PyArguments arguments)
    {
        var sepObj = arguments.Kwargs["sep"];
        if (!Utils.TryGetValue(sepObj, (PyStrObject str) => str.Value, "\n", out var sep))
            return PyVirtualMachine.RaiseTypeError($"end must be None or a string, not {sepObj.PyType.Name}");

        var endObj = arguments.Kwargs["end"];
        if (!Utils.TryGetValue(endObj, (PyStrObject str) => str.Value, "\n", out var end))
            return PyVirtualMachine.RaiseTypeError($"end must be None or a string, not {endObj.PyType.Name}");

        if (!PySpecialMethods.TryGetBool(arguments.Kwargs["flush"], out var flushObj))
            return null;

        for (int i = 0; i < arguments.ExtraArgs.Count; i++)
        {
            if (i is not 0)
                PyVirtualMachine.Out.Write(sep);

            if (PySpecialMethods.TryGetStr(arguments.ExtraArgs[i], out var str))
                PyVirtualMachine.Out.Write(str.Value);
            else
                return null;
        }
        PyVirtualMachine.Out.Write(end);
        if (flushObj.BoolValue)
            PyVirtualMachine.Out.Flush();

        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("base", "exp", "mod=None")]
    private static PyObject? PowImpl(PyArguments arguments)
    {
        var baseObj = arguments.Args[0];
        var expObj = arguments.Args[1];
        var modObj = arguments.Args[2];

        var result = PyOperators.Pow(baseObj, expObj, modObj);
        if (result is null)
            return null;

        if (result is PyNotImplementedObject)
            return PyVirtualMachine.RaiseTypeError($"unsupported operand type(s) for ** or pow(): '{baseObj.PyType.Name}', '{expObj.PyType.Name}', 'int'");

        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? DivModImpl(PyArguments arguments)
    {
        return PySpecialMethods.DivMod(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef()]
    private static PyStrObject? InputImpl_1(PyArguments arguments)
    {
        return PyStrObject.FromString(PyVirtualMachine.In.ReadLine() ?? string.Empty);
    }
    [PyFunctionArgsDef("prompt", "/")]
    private static PyStrObject? InputImpl_2(PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetStr(arguments.Args[0], out var s))
            return null;

        PyVirtualMachine.Out.Write(s.Value);

        return PyStrObject.FromString(PyVirtualMachine.In.ReadLine() ?? string.Empty);
    }
    [PyFunctionArgsDef("source", "/", "globals=None", "locals=None")]
    private static PyObject? EvalImpl(PyArguments arguments)
    {
        if (arguments.Args[0] is not PyStrObject str)
            return PyVirtualMachine.RaiseTypeError(null);

        var parser = new Parser("<string>", PyVirtualMachine.PyEnvironment.OptimizationOptions, Lexer.Tokenize(str.Value));
        var node = parser.ParseExpressionNode();

        var frame = PyVirtualMachine.CurrentFrame;
        var tempFrame = frame.TempFrame(FrameType.Eval);
        return node.Body.GetExprValue(tempFrame);
    }
    [PyFunctionArgsDef("source", "/", "globals=None", "locals=None", "*", "closure=None")]
    private static PyObject? ExecImpl(PyArguments arguments)
    {
        if (arguments.Args[0] is not PyStrObject str)
            return PyVirtualMachine.RaiseTypeError(null);

        ModuleNode node;
        try
        {
            var tokens = Lexer.Tokenize(str.Value);
            node = Parser.Parse("<string>", tokens, PyVirtualMachine.PyEnvironment);
        }
        catch (TokenizationException)
        {
            PyVirtualMachine.RaiseSyntaxError(null);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }
        catch (AstException)
        {
            PyVirtualMachine.RaiseSyntaxError(null);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        var frame = PyVirtualMachine.CurrentFrame;
        var tempFrame = frame.TempFrame(FrameType.Exec);
        node.Execute(tempFrame);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("iterable")]
    private static PyBoolObject? AllImpl(PyArguments arguments)
    {
        var iterable = arguments.Args[0];
        var elements = Utils.EnumerateIterable(iterable);
        if (elements is null)
            return null;

        foreach (var element in elements)
        {
            if (element is null)
                return null;

            if (!PySpecialMethods.TryGetBool(element, out var value))
                return null;

            if (!value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    [PyFunctionArgsDef("iterable")]
    private static PyBoolObject? AnyImpl(PyArguments arguments)
    {
        var iterable = arguments.Args[0];
        var elements = Utils.EnumerateIterable(iterable);
        if (elements is null)
            return null;

        foreach (var element in elements)
        {
            if (element is null)
                return null;

            if (!PySpecialMethods.TryGetBool(element, out var value))
                return null;

            if (value.BoolValue)
                return PyBoolObject.True;
        }

        return PyBoolObject.False;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "key=None")]
    private static PyObject? MaxImpl_1(PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        var elements = Utils.EnumerateIterable(iterable);
        if (elements is null)
            return null;

        PyObject? result = null;

        foreach (var element in elements)
        {
            if (element is null)
                return null;

            if (result is null)
            {
                result = element;
                continue;
            }

            var gt = PyOperators.Gt(element, result);
            if (gt is null)
                return null;

            if (!PySpecialMethods.TryGetBool(gt, out var b))
                return null;

            if (b.BoolValue)
                result = element;
        }

        if (result is null)
            PyVirtualMachine.RaiseValueError("max() iterable argument is empty");
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "default", "key=None")]
    private static PyObject? MaxImpl_2(PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        var elements = Utils.EnumerateIterable(iterable);
        if (elements is null)
            return null;

        PyObject result = arguments["default"];

        foreach (var element in elements)
        {
            if (element is null)
                return null;

            if (result is null)
            {
                result = element;
                continue;
            }

            var gt = PyOperators.Gt(element, result);
            if (gt is null)
                return null;

            if (!PySpecialMethods.TryGetBool(gt, out var b))
                return null;

            if (b.BoolValue)
                result = element;
        }

        return result;
    }

    [PyFunctionArgsDef("*args", "key=None")]
    private static PyObject? MaxImpl_3(PyArguments arguments)
    {
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        PyObject? result = null;

        foreach (var element in arguments.ExtraArgs)
        {
            if (element is null)
                return null;

            if (result is null)
            {
                result = element;
                continue;
            }

            var gt = PyOperators.Gt(element, result);
            if (gt is null)
                return null;

            if (!PySpecialMethods.TryGetBool(gt, out var b))
                return null;

            if (b.BoolValue)
                result = element;
        }

        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "key=None")]
    private static PyObject? MinImpl_1(PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        var elements = Utils.EnumerateIterable(iterable);
        if (elements is null)
            return null;

        PyObject? result = null;

        foreach (var element in elements)
        {
            if (element is null)
                return null;

            if (result is null)
            {
                result = element;
                continue;
            }

            var lt = PyOperators.Lt(element, result);
            if (lt is null)
                return null;

            if (!PySpecialMethods.TryGetBool(lt, out var b))
                return null;

            if (b.BoolValue)
                result = element;
        }

        if (result is null)
            PyVirtualMachine.RaiseValueError("min() iterable argument is empty");
        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "*", "default", "key=None")]
    private static PyObject? MinImpl_2(PyArguments arguments)
    {
        var iterable = arguments[0];
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        var elements = Utils.EnumerateIterable(iterable);
        if (elements is null)
            return null;

        PyObject result = arguments["default"];

        foreach (var element in elements)
        {
            if (element is null)
                return null;

            if (result is null)
            {
                result = element;
                continue;
            }

            var lt = PyOperators.Lt(element, result);
            if (lt is null)
                return null;

            if (!PySpecialMethods.TryGetBool(lt, out var b))
                return null;

            if (b.BoolValue)
                result = element;
        }

        return result;
    }

    [PyFunctionArgsDef("*args", "key=None")]
    private static PyObject? MinImpl_3(PyArguments arguments)
    {
        if (arguments["key"] is not PyNoneObject)
            throw new NotImplementedException();

        PyObject? result = null;

        foreach (var element in arguments.ExtraArgs)
        {
            if (element is null)
                return null;

            if (result is null)
            {
                result = element;
                continue;
            }

            var lt = PyOperators.Lt(element, result);
            if (lt is null)
                return null;

            if (!PySpecialMethods.TryGetBool(lt, out var b))
                return null;

            if (b.BoolValue)
                result = element;
        }

        return result;
    }

    [PyFunctionArgsDef("iterable", "/", "start=0")]
    private static PyObject? SumImpl(PyArguments arguments)
    {
        var start = arguments["start"];
        if (start is PyStrObject)
            return PyVirtualMachine.RaiseTypeError("sum() can't sum strings [use ''.join(seq) instead]");

        var iterable = Utils.EnumerateIterable(arguments[0]);
        if (iterable is null)
            return null;

        var result = start;
        foreach (var item in iterable)
        {
            if (item is null)
                return null;

            result = PyOperators.Add(result, item);
            if (result is null)
                return null;
        }

        return result;
    }

    [PyFunctionArgsDef("object", "name", "/")]
    private static PyObject? GetAttrImpl_1(PyArguments arguments)
    {
        var obj = arguments[0];
        if (!Utils.TryCastStrAsArg(arguments[1], out var name, "attribute name"))
            return null;

        return PyOperators.GetAttr(obj, name);
    }

    [PyFunctionArgsDef("object", "name", "default", "/")]
    private static PyObject? GetAttrImpl_2(PyArguments arguments)
    {
        var obj = arguments[0];
        if (!Utils.TryCastStrAsArg(arguments[1], out var name, "attribute name"))
            return null;

        var attr = PyOperators.GetAttr(obj, name);
        if (attr is not null || !PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.AttributeError))
            return attr;

        PyVirtualMachine.ClearException();
        return arguments[2];
    }

    [PyFunctionArgsDef("object", "name", "value", "/")]
    private static PyObject? SetAttrImpl(PyArguments arguments)
    {
        if (!Utils.TryCastStrAsArg(arguments[1], out var name, "attribute name"))
            return null;

        return PyOperators.SetAttr(arguments[0], name, arguments[2]);
    }

    [PyFunctionArgsDef("object", "name", "/")]
    private static PyBoolObject? HasAttrImpl(PyArguments arguments)
    {
        if (!Utils.TryCastStrAsArg(arguments[1], out var name, "attribute name"))
            return null;

        var attr = PyOperators.GetAttr(arguments[0], name);
        if (attr is not null)
            return PyBoolObject.True;

        if (!PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.AttributeError))
            return null;

        PyVirtualMachine.ClearException();
        return PyBoolObject.False;
    }

    [PyFunctionArgsDef()]
    private static PyListObject DirImpl_1(PyArguments arguments)
    {
        return PyListObject.CreateList(PyVirtualMachine.CurrentFrame.Locals
            .Concat(PyVirtualMachine.CurrentFrame.Closures.Select(static pair => KeyValuePair.Create(pair.Key, pair.Value.Value)))
            .Where(static pair => pair.Value is not null)
            .Select(static pair => PyStrObject.FromString(pair.Key)));
    }
    [PyFunctionArgsDef("object", "/")]
    private static PyListObject DirImpl_2(PyArguments arguments)
    {
        List<string> attrs = [];

        var obj = arguments[0];
        attrs.AddRange(obj.PyAttributes.Keys);

        foreach (var type in obj.PyType.MRO)
            attrs.AddRange(type.PyAttributes.Keys);

        return PyListObject.CreateList(attrs.Distinct().Order().Select(PyStrObject.FromString));
    }

    [PyFunctionArgsDef("codepoint", "/")]
    private static PyObject? ChrImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out var value))
            return null;

        if (!Rune.TryCreate(value, out var rune))
            return PyVirtualMachine.RaiseValueError("chr() arg not in range(0x110000)");

        return PyStrObject.FromRune(rune);
    }

    [PyFunctionArgsDef("c", "/")]
    private static PyObject? OrdImpl(PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyVirtualMachine.RaiseTypeError($"ord() expected string of length 1, but {arguments[0].PyType.Name} found");

        if (strObj.PyLength is not 1)
            return PyVirtualMachine.RaiseTypeError($"ord() expected a character, but string of length {strObj.PyLength} found");

        var successful = Rune.TryGetRuneAt(strObj.Value, 0, out var rune);
        Debug.Assert(successful);
        return PyIntObject.FromInteger(rune.Value);
    }

    [PyFunctionArgsDef()]
    private static PyDictObject LocalsImpl(PyArguments arguments)
    {
        return PyDictObject.CreateDict(PyVirtualMachine.CurrentFrame.Locals
            .Concat(PyVirtualMachine.CurrentFrame.Closures.Select(static pair => KeyValuePair.Create(pair.Key, pair.Value.Value)))
            .Where(static pair => pair.Value is not null)
            .Select(static pair => KeyValuePair.Create((PyObject)PyStrObject.FromString(pair.Key), pair.Value!)));
    }

    [PyFunctionArgsDef()]
    private static PyDictObject GlobalsImpl(PyArguments arguments)
    {
        return PyDictObject.CreateProxy(PyVirtualMachine.CurrentFrame.GlobalsAdapter);
    }

    [PyFunctionArgsDef("name", "globals=None", "locals=None", "fromlist=()", "level=0")]
    private static PyObject? ImportImpl(PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyVirtualMachine.RaiseTypeError("module name must be a string");

        var name = strObj.Value;
        if (!PyVirtualMachine.PyEnvironment.TryLoadModule(name, out var module))
            return PyVirtualMachine.RaiseException(PyStandardExceptionTypes.ModuleNotFoundError, $"No module named '{name}'");

        return module;
    }

    [PyFunctionArgsDef("object", "classinfo", "/")]
    private static PyBoolObject? IsInstanceImpl(PyArguments arguments)
    {
        var ret = IsInstanceForUnknown(arguments[0], arguments[1]);
        if (ret is null)
            return null;
        return PyBoolObject.FromBoolean(ret.Value);

        static bool? IsInstanceForUnknown(PyObject obj, PyObject classInfo)
        {
            return classInfo switch
            {
                PyTypeObject type => IsInstanceForType(obj, type),
                PyTupleObject types => IsInstanceForTuple(obj, types),
                _ => (bool?)(object?)PyVirtualMachine.RaiseTypeError("isinstance() arg 2 must be a type or a tuple of types")
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
    private static PyObject? IsSubclassImpl(PyArguments arguments)
    {
        if (arguments[0] is not PyTypeObject typeObj)
            return PyVirtualMachine.RaiseTypeError("issubclass() arg 1 must be a class");

        var ret = IsSubclassForUnknown(typeObj, arguments[1]);
        if (ret is null)
            return null;
        return PyBoolObject.FromBoolean(ret.Value);

        static bool? IsSubclassForUnknown(PyTypeObject obj, PyObject classInfo)
        {
            return classInfo switch
            {
                PyTypeObject type => IsSubclassForType(obj, type),
                PyTupleObject types => IsSubclassForTuple(obj, types),
                _ => (bool?)(object?)PyVirtualMachine.RaiseTypeError("issubclass() arg 2 must be a type or a tuple of types")
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
    private static PyBoolObject? CallableImpl(PyArguments arguments)
    {
        var attr = PyOperators.GetAttr(arguments[0], PySpecialNames.Call);
        if (attr is not null)
            return PyBoolObject.True;

        if (!PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.AttributeError))
            return null;

        PyVirtualMachine.ClearException();
        return PyBoolObject.False;
    }

    [PyFunctionArgsDef("object", "/")]
    private static PyIntObject IdImpl(PyArguments arguments)
    {
        return PyIntObject.FromInteger(arguments[0].PyId);
    }
}