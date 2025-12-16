using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{
    public virtual PyObject? Init(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyNoneObject.None;
    }

    public virtual PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Repr()
    {
        return PyStrObject.FromString($"<{PyType.Name} object at 0x{PyId:X16}>");
    }

    public virtual PyObject? Str()
    {
        return PySpecialCaller.Repr(this);
    }

    public virtual PyObject? Hash()
    {
        return PyIntObject.FromInteger(PyId);
    }

    public virtual PyObject? GetAttribute(string item)
    {
        return PyObjectGetAttribute(this, item);
    }

    public virtual PyObject? GetAttr(string item)
    {
        return PyVirtualMachine.RaiseAttributeError($"'{PyType.Name}' object has no attribute '{item}'");
    }

    public virtual PyObject? SetAttr(string key, PyObject value)
    {
        return PyObjectSetAttribute(this, key, value);
    }

    public virtual PyObject? DelAttr(string item)
    {
        return PyObjectDeleteAttribute(this, item);
    }

    public virtual PyObject? Bool()
    {
        return PyBoolObject.True;
    }

    public virtual PyObject? Int()
    {
        var index = Index();
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        return i;
    }
    public virtual PyObject? Float()
    {
        var index = Index();
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        return PyFloatObject.FromDouble((double)i.Value);
    }
    public virtual PyObject? Complex()
    {
        var index = Index();
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        throw new NotImplementedException();
    }

    public virtual PyObject? Index()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Contains(PyObject item)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? GetItem(PyObject item)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? SetItem(PyObject key, PyObject value)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? DelItem(PyObject key)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Len()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Iter()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Next()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Neg()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Pos()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Invert()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Abs()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    public virtual PyObject? Add(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Sub(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Mul(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? TrueDiv(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? FloorDiv(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Mod(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? DivMod(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Pow(PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? LShift(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RShift(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? And(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Xor(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Or(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    public virtual PyObject? RAdd(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RSub(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RMul(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RTrueDiv(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RFloorDiv(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RMod(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RDivMod(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RPow(PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RLShift(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RRShift(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RAnd(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? RXor(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? ROr(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    public virtual PyObject? Lt(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Le(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Eq(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Ne(PyObject other)
    {
        var eq = Eq(other);
        if (eq is null)
            return null;

        if (PySpecialMethods.TryGetBool(eq, out var b))
            return b.BoolValue ? PyBoolObject.False : PyBoolObject.True;

        return null;
    }
    public virtual PyObject? Gt(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    public virtual PyObject? Ge(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    public virtual PyObject? Missing(PyObject key)
    {
        return PyVirtualMachine.RaiseKeyError(key);
    }

    public virtual PyObject? Get(PyObject instance, PyObject owner)
    {
        throw new NotImplementedException();
    }

    public virtual PyObject? Set(PyObject instance, PyObject value)
    {
        throw new NotImplementedException();
    }

    public virtual PyObject? Delete(PyObject instance)
    {
        throw new NotImplementedException();
    }

    public virtual PyObject? SetName(PyObject owner, PyObject name)
    {
        return PyNoneObject.None;
    }
}
