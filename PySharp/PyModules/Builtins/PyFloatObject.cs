using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Numerics;

namespace PySharp.PyModules.Builtins;

public class PyFloatObject : PyObject
{
    public double Value { get; set; }

    public override PyTypeObject PyType => PyBuiltinTypes.Float;

    public PyFloatObject()
    {
    }

    public PyFloatObject(double value) : this()
    {
        Value = value;
    }

    public static PyFloatObject FromDouble(double value)
    {
        return new PyFloatObject(value);
    }

    public static implicit operator PyFloatObject(double value)
    {
        return new PyFloatObject(value);
    }

    public override PyStrObject Repr()
    {
        return PyStrObject.FromString(Value.ToString());
    }

    public override PyObject? Hash()
    {
        return PyIntObject.FromInteger(Value.GetHashCode());
    }

    public override PyBoolObject Bool()
    {
        return Value is not 0;
    }

    public override PyObject? Int()
    {
        return new PyIntObject((BigInteger)Value);
    }

    public override PyObject? Float()
    {
        return this;
    }

    public override PyObject? Neg()
    {
        return FromDouble(-Value);
    }

    public override PyObject? Pos()
    {
        return this;
    }

    public override PyObject? Abs()
    {
        return Value >= 0 ? this : Neg();
    }

    public override PyObject? Add(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value + (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value + floatObj.Value),
            _ => base.Add(other),
        };
    }
    public override PyObject? Sub(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value - (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value - floatObj.Value),
            _ => base.Sub(other),
        };
    }
    public override PyObject? Mul(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value * (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value * floatObj.Value),
            _ => base.Mul(other),
        };
    }
    public override PyObject? TrueDiv(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value / (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value / floatObj.Value),
            _ => base.TrueDiv(other),
        };
    }
    public override PyObject? FloorDiv(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(double.Floor(Value / (double)intObj.Value)),
            PyFloatObject floatObj => FromDouble(double.Floor(Value / floatObj.Value)),
            _ => base.FloorDiv(other),
        };
    }
    public override PyObject? DivMod(PyObject other)
    {
        var q = FloorDiv(other);
        if (q is null)
            return null;

        if (q is PyNotImplementedObject)
            return PyNotImplementedObject.NotImplemented;

        var r = Mod(other);
        if (r is null)
            return null;

        if (r is PyNotImplementedObject)
            return PyNotImplementedObject.NotImplemented;

        return PyTupleObject.CreateTuple(q, r);
    }
    public override PyObject? Mod(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value % (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value % floatObj.Value),
            _ => base.Mod(other),
        };
    }
    public override PyObject? Pow(PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyNotImplementedObject.NotImplemented;

        return other switch
        {
            PyIntObject intObj => FromDouble(double.Pow(Value, (double)intObj.Value)),
            PyFloatObject floatObj => FromDouble(double.Pow(Value, floatObj.Value)),
            _ => base.Pow(other, modulo),
        };
    }

    public override PyObject? RAdd(PyObject other)
    {
        return Add(other);
    }
    public override PyObject? RSub(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble((double)intObj.Value - Value),
            PyFloatObject floatObj => FromDouble(floatObj.Value - Value),
            _ => base.RSub(other),
        };
    }
    public override PyObject? RMul(PyObject other)
    {
        return Mul(other);
    }
    public override PyObject? RTrueDiv(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble((double)intObj.Value / Value),
            PyFloatObject floatObj => FromDouble(floatObj.Value / Value),
            _ => base.RTrueDiv(other),
        };
    }
    public override PyObject? RFloorDiv(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(double.Floor((double)intObj.Value / Value)),
            PyFloatObject floatObj => FromDouble(double.Floor(floatObj.Value / Value)),
            _ => base.RFloorDiv(other),
        };
    }
    public override PyObject? RDivMod(PyObject other)
    {
        var q = RFloorDiv(other);
        if (q is null)
            return null;

        if (q is PyNotImplementedObject)
            return PyNotImplementedObject.NotImplemented;

        var r = RMod(other);
        if (r is null)
            return null;

        if (r is PyNotImplementedObject)
            return PyNotImplementedObject.NotImplemented;

        return PyTupleObject.CreateTuple(q, r);
    }
    public override PyObject? RMod(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble((double)intObj.Value % Value),
            PyFloatObject floatObj => FromDouble(floatObj.Value % Value),
            _ => base.Mod(other),
        };
    }
    public override PyObject? RPow(PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyNotImplementedObject.NotImplemented;

        return other switch
        {
            PyIntObject intObj => FromDouble(double.Pow((double)intObj.Value, Value)),
            PyFloatObject floatObj => FromDouble(double.Pow(floatObj.Value, Value)),
            _ => base.Pow(other, modulo),
        };
    }

    public override PyObject? Lt(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value < (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value < floatObj.Value),
            _ => base.Lt(other),
        };
    }
    public override PyObject? Gt(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value > (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value > floatObj.Value),
            _ => base.Gt(other),
        };
    }
    public override PyObject? Le(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value <= (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value <= floatObj.Value),
            _ => base.Le(other),
        };
    }
    public override PyObject? Ge(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value >= (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value >= floatObj.Value),
            _ => base.Ge(other),
        };
    }
    public override PyObject? Eq(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value == (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value == floatObj.Value),
            _ => base.Eq(other),
        };
    }
}

public sealed class PyFloatObjectType : PyTypeObject
{
    public override string Name => "float";

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        return PySpecialMethods.GetFloat(pack[0]);
    }
}