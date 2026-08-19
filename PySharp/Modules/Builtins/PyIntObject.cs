using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.Modules.Builtins;

public class PyIntObject : PyObject
{
    internal const int NegativePoolSize = 6;
    internal const int PositivesPoolSize = 257;

    internal static readonly PyIntObject[] NegativeInts;
    internal static readonly PyIntObject[] PositiveInts;

    public static PyIntObject Zero { get; }
    public static PyIntObject One { get; }
    public static PyIntObject MinusOne { get; }

    static PyIntObject()
    {
        NegativeInts = new PyIntObject[NegativePoolSize];
        PositiveInts = new PyIntObject[PositivesPoolSize];

        for (int i = 0; i < NegativeInts.Length; i++)
            NegativeInts[i] = new PyIntObject(-i);
        for (int i = 0; i < PositiveInts.Length; i++)
            PositiveInts[i] = new PyIntObject(i);

        Zero = PositiveInts[0];
        One = PositiveInts[1];
        MinusOne = NegativeInts[1];
    }

    public override PyTypeObject DefaultPyType => PyIntObjectType.Shared;

    public BigInteger Value { get; }
    public bool IsInt32 => Value >= int.MinValue && Value <= int.MaxValue;
    public int Int32Value => IsInt32 ? (int)Value : throw new PyRuntimeException(PyOverflowErrorObjectType.Shared.Create());

    internal PyIntObject(BigInteger value)
    {
        Value = value;
    }


    public static PyIntObject FromInteger(int value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return PositiveInts[value];

            if (value > -NegativePoolSize)
                return NegativeInts[-value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromInteger(long value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return PositiveInts[value];

            if (value > -NegativePoolSize)
                return NegativeInts[-value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromInteger(BigInteger value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return PositiveInts[(int)value];

            if (value > -NegativePoolSize)
                return NegativeInts[-(int)value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromIntegerNoCache(BigInteger value)
    {
        return new PyIntObject(value);
    }
}

[PyType("int")]
public sealed partial class PyIntObjectType : PyTypeObject<PyIntObject>
{

    [PyExport(PySpecialNames.New, nameof(NewImpl_1), nameof(NewImpl_2))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("number=0", "/")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject str)
        {
            if (!BigIntegerHelper.TryParse(str.Value, 10, out var integer))
                return PyResult.ValueError(PySR.Runtime_Number_Int_InvalidLiteral, 10, str.Value);

            return PyIntObject.FromInteger(integer);
        }

        var result = PySpecialMethods.Int(context, arguments[0]);
        if (result.IsError)
            return result;

        return result.Value;
    }
    [PyFunctionParameters("string", "/", "base=10")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        if (arguments[1] is not PyIntObject numBase)
            return PyResult.TypeError(PySR.Runtime_Number_Int_CannotInterpretedAsInt, arguments[1].PyType.FullName);

        if (!((numBase.Value >= 2 && numBase.Value <= 36) || numBase.Value.IsZero))
            return PyResult.ValueError(PySR.Runtime_Number_Int_BaseOutOfRange);

        if (arguments[0] is PyStrObject str)
        {
            if (!BigIntegerHelper.TryParse(str.Value, numBase.Int32Value, out var result))
                return PyResult.ValueError(PySR.Runtime_Number_Int_InvalidLiteral, numBase.Value, str.Value);

            return PyIntObject.FromInteger(result);
        }

        return PyResult.TypeError(PySR.Runtime_Number_Int_ConvertNonStr);

    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var result = _new.Call(context, args, kwargs);
        if (result.IsError)
            return result;

        var obj = result.Value;
        Debug.Assert(obj is PyIntObject);
        var value = ((PyIntObject)obj).Value;

        var eq = PyComparer.Eq(context, cls, this);
        if (eq.IsError)
            return eq.ExceptionResult;

        if (!eq.Value.BoolValue && value > -PyIntObject.NegativePoolSize && value < PyIntObject.PositivesPoolSize)
            obj = PyIntObject.FromIntegerNoCache(value);

        obj._pyType = cls;
        return obj;
    }

    protected override PyResult Index(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected override PyResult Hash(PyCallContext context, PyIntObject self)
    {
        // CPython long_hash: small ints hash to themselves (return self without
        // allocating), huge ints are reduced modulo 2**61-1 and stay consistent
        // with float hashes. hash(-1) == -2 (error sentinel).
        var hash = PyHash.HashLong(self.Value);
        // hash() must return the built-in int type, never an int subclass
        // instance: hash(MyInt(9)) has type int, and hash(True) has type int
        // too. Only the exact built-in int can be returned as-is.
        return hash == self.Value && self.PyType == PyIntObjectType.Shared
            ? self
            : PyIntObject.FromInteger(hash);
    }

    protected override PyResult Repr(PyCallContext context, PyIntObject self)
    {
        return PyStrObject.FromString(self.Value.ToString());
    }

    protected override PyResult Bool(PyCallContext context, PyIntObject self)
    {
        return PyBoolObject.FromBoolean(self.Value != 0);
    }

    protected override PyResult Int(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected override PyResult Float(PyCallContext context, PyIntObject self)
    {
        var d = (double)self.Value;
        if (double.IsInfinity(d))
            return PyResult.OverflowError("int too large to convert to float");
        return PyFloatObject.FromDouble(d);
    }

    protected override PyResult Neg(PyCallContext context, PyIntObject self)
    {
        return PyIntObject.FromInteger(-self.Value);
    }

    protected override PyResult Pos(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected override PyResult Abs(PyCallContext context, PyIntObject self)
    {
        return self.Value >= 0 ? self : PyIntObject.FromInteger(-self.Value);
    }

    protected override PyResult Invert(PyCallContext context, PyIntObject self)
    {
        return PyIntObject.FromInteger(~self.Value);
    }

    protected override PyResult Add(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Add, self, intObj);
    }
    protected override PyResult Sub(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Sub(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Sub, self, intObj);
    }
    protected override PyResult Mul(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mul(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mult, self, intObj);
    }
    protected override PyResult TrueDiv(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.TrueDiv(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.TrueDiv, self, intObj);
    }
    protected override PyResult FloorDiv(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.FloorDiv(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.FloorDiv, self, intObj);
    }
    protected override PyResult Mod(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mod(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mod, self, intObj);
    }
    protected override PyResult DivMod(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.DivMod(context, self, other);
        var (q, r) = BigInteger.DivRem(self.Value, intObj.Value);
        return PyTupleObject.CreateTuple(PyIntObject.FromInteger(q), PyIntObject.FromInteger(r));
    }
    protected override PyResult Pow(PyCallContext context, PyIntObject self, PyObject other, PyObject modulo)
    {
        if (other is not PyIntObject intObj)
            return base.Pow(context, self, other, modulo);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Pow, self, intObj, modulo);
    }
    protected override PyResult LShift(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.LShift(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.LShift, self, intObj);
    }
    protected override PyResult RShift(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.RShift(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.RShift, self, intObj);
    }
    protected override PyResult And(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.And(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.BitAnd, self, intObj);
    }
    protected override PyResult Xor(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Xor(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.BitXor, self, intObj);
    }
    protected override PyResult Or(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Or(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.BitOr, self, intObj);
    }
    protected override PyResult Lt(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Lt(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Lt, self, intObj);
    }
    protected override PyResult Gt(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Gt(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Gt, self, intObj);
    }
    protected override PyResult Le(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Le(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.LtE, self, intObj);
    }
    protected override PyResult Ge(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Ge(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.GtE, self, intObj);
    }
    protected override PyResult Eq(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Eq(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Eq, self, intObj);
    }

    [AIGenerated]
    protected override PyResult Format(PyCallContext context, PyIntObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.FullName);

        if (!PyFormatSpec.TryParse(str.Value, out var spec))
            return PyResult.ValueError(PySR.Runtime_Object_FormatSpecInvalid, str.Value, self.PyType.FullName);

        var val = self.Value;
        var formatType = spec.Type ?? 'd';

        string text;
        int numBase;
        string basePrefix = string.Empty;

        switch (char.ToLowerInvariant(formatType))
        {
            case 'b':
                numBase = 2;
                if (spec.AlternateForm)
                    basePrefix = char.IsUpper(formatType) ? "0B" : "0b";
                text = BigIntegerHelper.ToStringDigits(BigInteger.Abs(val), numBase);
                if (spec.WidthGrouping is not null)
                    text = ApplyGrouping(text, spec.WidthGrouping.Value, 4);
                break;
            case 'o':
                numBase = 8;
                if (spec.AlternateForm)
                    basePrefix = char.IsUpper(formatType) ? "0O" : "0o";
                text = BigIntegerHelper.ToStringDigits(BigInteger.Abs(val), numBase);
                break;
            case 'x':
                numBase = 16;
                if (spec.AlternateForm)
                    basePrefix = char.IsUpper(formatType) ? "0X" : "0x";
                text = BigIntegerHelper.ToStringDigits(BigInteger.Abs(val), numBase);
                if (char.IsUpper(formatType))
                    text = text.ToUpperInvariant();
                if (spec.WidthGrouping is not null)
                    text = ApplyGrouping(text, spec.WidthGrouping.Value, 4);
                break;
            case 'd':
            case 'n':
                text = BigInteger.Abs(val).ToString();
                if (spec.WidthGrouping is not null)
                    text = ApplyGrouping(text, spec.WidthGrouping.Value, 3);
                break;
            case 'c':
                if (val < 0 || val > 0x10FFFF)
                    return PyResult.OverflowError($"%c arg not in range(0x110000)");
                text = char.ConvertFromUtf32((int)val);
                break;
            case 'e':
            case 'f':
            case 'g':
            case '%':
                // fallback to float format
                return PySpecialMethods.Format(context, PyFloatObject.FromDouble((double)val), formatSpec);
            default:
                return PyResult.ValueError(PySR.Runtime_Object_FormatUnsupported, self.PyType.FullName);
        }

        var prefix = string.Empty;
        if (val < 0)
            prefix = "-";
        else if (spec.Sign is '+' or ' ')
            prefix = spec.Sign.Value.ToString();

        string fullPrefix = prefix + basePrefix;

        if (spec.Width is not null)
        {
            var width = spec.Width.Value;
            var fill = spec.Fill ?? (spec.SignAwareZeroPadding && spec.Align is null ? '0' : ' ');
            var align = spec.Align ?? (spec.SignAwareZeroPadding ? '=' : '>');
            text = ApplyWidth(fullPrefix, text, width, fill, align);
        }
        else
        {
            text = fullPrefix + text;
        }

        return PyStrObject.FromString(text);

        static string ApplyGrouping(string value, char grouping, int groupSize)
        {
            if (grouping is not ',' and not '_')
                return value;

            int len = value.Length;
            if (len <= groupSize)
                return value;

            // Calculate number of separators
            int sepCount = (len - 1) / groupSize;
            Span<char> span = stackalloc char[len + sepCount];

            int dst = span.Length - 1;
            int src = len - 1;
            int count = 0;

            while (src >= 0)
            {
                span[dst--] = value[src--];
                count++;
                if (count == groupSize && src >= 0)
                {
                    span[dst--] = grouping;
                    count = 0;
                }
            }
            return span.ToString();
        }

        static string ApplyWidth(string prefix, string body, int width, char fill, char align)
        {
            var text = prefix + body;
            if (text.Length >= width)
                return text;

            var padding = width - text.Length;
            return align switch
            {
                '<' => text.PadRight(width, fill),
                '^' => PadCenter(text, width, fill),
                '=' => prefix + body.PadLeft(body.Length + padding, fill),
                _ => text.PadLeft(width, fill),
            };
        }

        static string PadCenter(string text, int width, char fill)
        {
            var totalPadding = width - text.Length;
            var leftPadding = totalPadding / 2;
            return string.Create(width, (text, fill, leftPadding), static (span, state) =>
            {
                span[..state.leftPadding].Fill(state.fill);
                state.text.AsSpan().CopyTo(span[state.leftPadding..]);
                span[(state.leftPadding + state.text.Length)..].Fill(state.fill);
            });
        }
    }
}
