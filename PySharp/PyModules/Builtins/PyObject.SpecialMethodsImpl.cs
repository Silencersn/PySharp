using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{
    protected internal virtual PyObject? InitImpl(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyNoneObject.None;
    }

    protected internal virtual PyObject? CallImpl(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? ReprImpl()
    {
        return PyStrObject.FromString($"<{PyType.Name} object at 0x{PyId:X16}>");
    }

    protected internal virtual PyObject? StrImpl()
    {
        return Repr();
    }

    protected internal virtual PyObject? HashImpl()
    {
        return PyIntObject.FromInteger(PyId);
    }

    protected internal virtual PyObject? GetAttributeImpl(string item)
    {
        return PyObjectGetAttribute(this, item);
    }

    protected internal virtual PyObject? GetAttrImpl(string item)
    {
        return PyVirtualMachine.RaiseAttributeError($"'{PyType.Name}' object has no attribute '{item}'");
    }

    protected internal virtual PyObject? SetAttrImpl(string key, PyObject value)
    {
        return PyObjectSetAttribute(this, key, value);
    }

    protected internal virtual PyObject? DelAttrImpl(string item)
    {
        return PyObjectDeleteAttribute(this, item);
    }

    protected internal virtual PyObject? BoolImpl()
    {
        return PyBoolObject.True;
    }

    protected internal virtual PyObject? IntImpl()
    {
        // TOOD: is this implementation correct?
        var index = Index();
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        return i;
    }
    protected internal virtual PyObject? FloatImpl()
    {
        // TOOD: is this implementation correct?
        var index = Index();
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        return PyFloatObject.FromDouble((double)i.Value);
    }
    protected internal virtual PyObject? ComplexImpl()
    {
        // TOOD: is this implementation correct?
        var index = Index();
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        throw new NotImplementedException();
    }

    protected internal virtual PyObject? IndexImpl()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? ContainsImpl(PyObject item)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? GetItemImpl(PyObject item)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? SetItemImpl(PyObject key, PyObject value)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? DelItemImpl(PyObject key)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? LenImpl()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? IterImpl()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? NextImpl()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? NegImpl()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? PosImpl()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? InvertImpl()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? AbsImpl()
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? AddImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? SubImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? MulImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? TrueDivImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? FloorDivImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? ModImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? DivModImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? PowImpl(PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? LShiftImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RShiftImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? AndImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? XorImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? OrImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyObject? RAddImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RSubImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RMulImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RTrueDivImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RFloorDivImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RModImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RDivModImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RPowImpl(PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RLShiftImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RRShiftImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RAndImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RXorImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? ROrImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyObject? LtImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? LeImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? EqImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? NeImpl(PyObject other)
    {
        var eq = Eq(other);
        if (eq is null)
            return null;

        if (PySpecialMethods.TryGetBool(eq, out var b))
            return b.BoolValue ? PyBoolObject.False : PyBoolObject.True;

        return null;
    }
    protected internal virtual PyObject? GtImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? GeImpl(PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyObject? MissingImpl(PyObject key)
    {
        return PyVirtualMachine.RaiseKeyError(key);
    }

    protected internal virtual PyObject? GetImpl(PyObject instance, PyObject owner)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyObject? SetImpl(PyObject instance, PyObject value)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyObject? DeleteImpl(PyObject instance)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyObject? SetNameImpl(PyObject owner, PyObject name)
    {
        return PyNoneObject.None;
    }
}
