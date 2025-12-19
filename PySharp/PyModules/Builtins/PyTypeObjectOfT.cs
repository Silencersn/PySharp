using PySharp.PyRuntime;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyModules.Builtins;

public abstract class PyTypeObject<TObject> : PyTypeObject where TObject : PyObject
{
    public sealed override Type LayoutType => typeof(TObject);

    protected internal new virtual PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError($"cannot create '{Name}' instances");
    }

    protected internal virtual PyObject? Init(TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyNoneObject.None;
    }

    protected internal virtual PyObject? Call(TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Repr(TObject self)
    {
        return PyStrObject.FromString($"<{Name} object at 0x{self.PyId:X16}>");
    }

    protected internal virtual PyObject? Str(TObject self)
    {
        return Repr(self);
    }

    protected internal virtual PyObject? Hash(TObject self)
    {
        return PyIntObject.FromInteger(self.PyId);
    }

    protected internal virtual PyObject? GetAttribute(TObject self, string item)
    {
        return PyObjectGetAttribute(self, item);
    }

    protected internal virtual PyObject? GetAttr(TObject self, string item)
    {
        return PyVirtualMachine.RaiseAttributeError($"'{Name}' object has no attribute '{item}'");
    }

    protected internal virtual PyObject? SetAttr(TObject self, string key, PyObject value)
    {
        if (self.IsImmutable)
            return PyVirtualMachine.RaiseTypeError($"cannot set '{key}' attribute of immutable type '{Name}'");

        return PyObjectSetAttribute(self, key, value);
    }

    protected internal virtual PyObject? DelAttr(TObject self, string item)
    {
        if (self.IsImmutable)
            return PyVirtualMachine.RaiseTypeError($"cannot set '{item}' attribute of immutable type '{Name}'");

        return PyObjectDeleteAttribute(self, item);
    }

    protected internal virtual PyObject? Bool(TObject self)
    {
        return PyBoolObject.True;
    }

    protected internal virtual PyObject? Int(TObject self)
    {
        // TOOD: is this implementation correct?
        var index = Index(self);
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        return i;
    }
    protected internal virtual PyObject? Float(TObject self)
    {
        // TOOD: is this implementation correct?
        var index = Index(self);
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        return PyFloatObject.FromDouble((double)i.Value);
    }
    protected internal virtual PyObject? Complex(TObject self)
    {
        // TOOD: is this implementation correct?
        var index = Index(self);
        if (index is null)
            return null;

        if (!PySpecialMethods.TryGetIndex(index, out var i))
            return null;

        throw new NotImplementedException();
    }

    protected internal virtual PyObject? Index(TObject self)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Contains(TObject self, PyObject item)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? GetItem(TObject self, PyObject item)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? SetItem(TObject self, PyObject key, PyObject value)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? DelItem(TObject self, PyObject key)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Len(TObject self)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Iter(TObject self)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Next(TObject self)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Neg(TObject self)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Pos(TObject self)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Invert(TObject self)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Abs(TObject self)
    {
        return PyVirtualMachine.RaiseTypeError(null);
    }

    protected internal virtual PyObject? Add(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Sub(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Mul(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? TrueDiv(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? FloorDiv(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Mod(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? DivMod(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Pow(TObject self, PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? LShift(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RShift(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? And(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Xor(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Or(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyObject? RAdd(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RSub(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RMul(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RTrueDiv(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RFloorDiv(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RMod(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RDivMod(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RPow(TObject self, PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RLShift(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RRShift(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RAnd(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? RXor(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? ROr(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyObject? Lt(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Le(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Eq(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Ne(TObject self, PyObject other)
    {
        var eq = Eq(self, other);
        if (eq is null)
            return null;

        if (PySpecialMethods.TryGetBool(eq, out var b))
            return b.BoolValue ? PyBoolObject.False : PyBoolObject.True;

        return null;
    }
    protected internal virtual PyObject? Gt(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyObject? Ge(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyObject? Missing(TObject self, PyObject key)
    {
        return PyVirtualMachine.RaiseKeyError(key);
    }

    protected internal virtual PyObject? Get(TObject self, PyObject instance, PyObject owner)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyObject? Set(TObject self, PyObject instance, PyObject value)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyObject? Delete(TObject self, PyObject instance)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyObject? SetName(TObject self, PyObject owner, PyObject name)
    {
        return PyNoneObject.None;
    }

    protected internal virtual PyObject? Format(TObject self, string formatSpec)
    {
        if (formatSpec.Length is 0)
            return Str(self);

        return PyVirtualMachine.RaiseTypeError($"unsupported format string passed to {Name}.__format__");
    }

}
