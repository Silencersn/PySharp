using PySharp.AstNodes;
using PySharp.PyRuntime;
using PySharp.PyRuntime.PyAttributes;
using PySharp.Tokenization;

namespace PySharp.PyObjects.Builtins;

public static partial class PyBuiltinFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Repr = new("repr", PySpecialMethods.GetRepr);
    public static readonly PyBuiltinFunctionOrMethodObject Len = new("len", PySpecialMethods.GetLen);
    public static readonly PyBuiltinFunctionOrMethodObject Hash = new("hash", PySpecialMethods.GetHash);
    public static readonly PyBuiltinFunctionOrMethodObject Abs = new("abs", PySpecialMethods.Abs);
    public static readonly PyBuiltinFunctionOrMethodObject Iter = new("iter", PySpecialMethods.Iter);
    public static readonly PyBuiltinFunctionOrMethodObject Next = new("next", PySpecialMethods.Next);
    public static readonly PyBuiltinFunctionOrMethodObject Print = new("print", PrintImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Pow = new("pow", PowImpl);
    public static readonly PyBuiltinFunctionOrMethodObject DivMod = new("divmod", DivModImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Input = new("input", InputImpl_1, InputImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject Eval = new("eval", EvalImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Exec = new("exec", ExecImpl);
    public static readonly PyBuiltinFunctionOrMethodObject All = new("all", AllImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Any = new("any", AnyImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Max = new("max", MaxImpl_1, MaxImpl_2, MaxImpl_3);
    public static readonly PyBuiltinFunctionOrMethodObject Sum = new("sum", SumImpl);
    public static readonly PyBuiltinFunctionOrMethodObject GetAttr = new("getattr", GetAttrImpl_1, GetAttrImpl_2);

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

        var parser = new Parser(Lexer.Tokenize(str.Value));
        var node = parser.ParseExpressionNode();

        var frame = PyVirtualMachine.CurrentFrame;
        var tempFrame = frame.TempFrame();
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
            node = Parser.Parse(tokens, PyVirtualMachine.PyEnvironment);
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
        var tempFrame = frame.TempFrame();
        node.Execute(tempFrame);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("iterable")]
    private static PyBoolObject? AllImpl(PyArguments arguments)
    {
        var iterable = arguments.Args[0];
        var elements = Utils.EnumerableIterable(iterable);
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
        var elements = Utils.EnumerableIterable(iterable);
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

        var elements = Utils.EnumerableIterable(iterable);
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

        var elements = Utils.EnumerableIterable(iterable);
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

    [PyFunctionArgsDef("iterable", "/", "start=0")]
    private static PyObject? SumImpl(PyArguments arguments)
    {
        var start = arguments["start"];
        if (start is PyStrObject)
            return PyVirtualMachine.RaiseTypeError("sum() can't sum strings [use ''.join(seq) instead]");

        var iterable = Utils.EnumerableIterable(arguments[0]);
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
        if (attr is not null || PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.AttributeError))
            return attr;

        return arguments[2];
    }
}