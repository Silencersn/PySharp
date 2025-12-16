using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Numerics;

namespace PySharp.PyModules.Builtins;

public class PyFloatObject : PyObject
{
    public double Value { get; set; }

    public override PyTypeObject DefaultPyType => PyFloatObjectType.Shared;

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

    protected internal override PyObject? ReprImpl()
    {
        return PyStrObject.FromString(Value.ToString());
    }

    protected internal override PyObject? HashImpl()
    {
        return PyIntObject.FromInteger(Value.GetHashCode());
    }

    protected internal override PyObject? BoolImpl()
    {
        return PyBoolObject.FromBoolean(Value is not 0);
    }

    protected internal override PyObject? IntImpl()
    {
        return new PyIntObject((BigInteger)Value);
    }

    protected internal override PyObject? FloatImpl()
    {
        return this;
    }

    protected internal override PyObject? NegImpl()
    {
        return FromDouble(-Value);
    }

    protected internal override PyObject? PosImpl()
    {
        return this;
    }

    protected internal override PyObject? AbsImpl()
    {
        return Value >= 0 ? this : NegImpl();
    }

    protected internal override PyObject? AddImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value + (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value + floatObj.Value),
            _ => base.AddImpl(other),
        };
    }
    protected internal override PyObject? SubImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value - (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value - floatObj.Value),
            _ => base.SubImpl(other),
        };
    }
    protected internal override PyObject? MulImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value * (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value * floatObj.Value),
            _ => base.MulImpl(other),
        };
    }
    protected internal override PyObject? TrueDivImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value / (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value / floatObj.Value),
            _ => base.TrueDivImpl(other),
        };
    }
    protected internal override PyObject? FloorDivImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(double.Floor(Value / (double)intObj.Value)),
            PyFloatObject floatObj => FromDouble(double.Floor(Value / floatObj.Value)),
            _ => base.FloorDivImpl(other),
        };
    }
    protected internal override PyObject? DivModImpl(PyObject other)
    {
        var q = FloorDivImpl(other);
        if (q is null)
            return null;

        if (q is PyNotImplementedObject)
            return PyNotImplementedObject.NotImplemented;

        var r = ModImpl(other);
        if (r is null)
            return null;

        if (r is PyNotImplementedObject)
            return PyNotImplementedObject.NotImplemented;

        return PyTupleObject.CreateTuple(q, r);
    }
    protected internal override PyObject? ModImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(Value % (double)intObj.Value),
            PyFloatObject floatObj => FromDouble(Value % floatObj.Value),
            _ => base.ModImpl(other),
        };
    }
    protected internal override PyObject? PowImpl(PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyNotImplementedObject.NotImplemented;

        return other switch
        {
            PyIntObject intObj => FromDouble(double.Pow(Value, (double)intObj.Value)),
            PyFloatObject floatObj => FromDouble(double.Pow(Value, floatObj.Value)),
            _ => base.PowImpl(other, modulo),
        };
    }

    protected internal override PyObject? RAddImpl(PyObject other)
    {
        return AddImpl(other);
    }
    protected internal override PyObject? RSubImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble((double)intObj.Value - Value),
            PyFloatObject floatObj => FromDouble(floatObj.Value - Value),
            _ => base.RSubImpl(other),
        };
    }
    protected internal override PyObject? RMulImpl(PyObject other)
    {
        return MulImpl(other);
    }
    protected internal override PyObject? RTrueDivImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble((double)intObj.Value / Value),
            PyFloatObject floatObj => FromDouble(floatObj.Value / Value),
            _ => base.RTrueDivImpl(other),
        };
    }
    protected internal override PyObject? RFloorDivImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble(double.Floor((double)intObj.Value / Value)),
            PyFloatObject floatObj => FromDouble(double.Floor(floatObj.Value / Value)),
            _ => base.RFloorDivImpl(other),
        };
    }
    protected internal override PyObject? RDivModImpl(PyObject other)
    {
        var q = RFloorDivImpl(other);
        if (q is null)
            return null;

        if (q is PyNotImplementedObject)
            return PyNotImplementedObject.NotImplemented;

        var r = RModImpl(other);
        if (r is null)
            return null;

        if (r is PyNotImplementedObject)
            return PyNotImplementedObject.NotImplemented;

        return PyTupleObject.CreateTuple(q, r);
    }
    protected internal override PyObject? RModImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => FromDouble((double)intObj.Value % Value),
            PyFloatObject floatObj => FromDouble(floatObj.Value % Value),
            _ => base.ModImpl(other),
        };
    }
    protected internal override PyObject? RPowImpl(PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyNotImplementedObject.NotImplemented;

        return other switch
        {
            PyIntObject intObj => FromDouble(double.Pow((double)intObj.Value, Value)),
            PyFloatObject floatObj => FromDouble(double.Pow(floatObj.Value, Value)),
            _ => base.PowImpl(other, modulo),
        };
    }

    protected internal override PyObject? LtImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value < (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value < floatObj.Value),
            _ => base.LtImpl(other),
        };
    }
    protected internal override PyObject? GtImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value > (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value > floatObj.Value),
            _ => base.GtImpl(other),
        };
    }
    protected internal override PyObject? LeImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value <= (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value <= floatObj.Value),
            _ => base.LeImpl(other),
        };
    }
    protected internal override PyObject? GeImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value >= (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value >= floatObj.Value),
            _ => base.GeImpl(other),
        };
    }
    protected internal override PyObject? EqImpl(PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(Value == (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(Value == floatObj.Value),
            _ => base.EqImpl(other),
        };
    }
}

public sealed class PyFloatObjectType : PyPrimitiveTypeObject<PyFloatObjectType, PyFloatObject>
{
    public override string Name => "float";

    protected internal override PyObject? NewImpl(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        return PySpecialMethods.GetFloat(pack[0]);
    }
}