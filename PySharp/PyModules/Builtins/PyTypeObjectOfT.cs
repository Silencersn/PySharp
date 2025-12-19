using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyModules.Builtins;

public abstract class PyTypeObject<TObject> : PyTypeObject where TObject : PyObject
{
    public sealed override Type LayoutType => typeof(TObject);

    protected internal new virtual PyResult New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError($"cannot create '{Name}' instances");
    }

    protected internal virtual PyResult Init(TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyNoneObject.None;
    }

    protected internal virtual PyResult Call(TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Repr(TObject self)
    {
        return PyStrObject.FromString($"<{Name} object at 0x{self.PyId:X16}>");
    }

    protected internal virtual PyResult Str(TObject self)
    {
        return Repr(self);
    }

    protected internal virtual PyResult Hash(TObject self)
    {
        return PyIntObject.FromInteger(self.PyId);
    }

    protected internal virtual PyResult GetAttribute(TObject self, string item)
    {
        return PyObjectGetAttribute(self, item) ?? PyResult.CaptureExceptionFromPVM();
    }

    protected internal virtual PyResult GetAttr(TObject self, string item)
    {
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{item}'");
    }

    protected internal virtual PyResult SetAttr(TObject self, string key, PyObject value)
    {
        if (self.IsImmutable)
            return PyResult.RaiseTypeError($"cannot set '{key}' attribute of immutable type '{Name}'");

        return PyObjectSetAttribute(self, key, value) ?? PyResult.CaptureExceptionFromPVM();
    }

    protected internal virtual PyResult DelAttr(TObject self, string item)
    {
        if (self.IsImmutable)
            return PyResult.RaiseTypeError($"cannot set '{item}' attribute of immutable type '{Name}'");

        return PyObjectDeleteAttribute(self, item) ?? PyResult.CaptureExceptionFromPVM();
    }

    protected internal virtual PyResult Bool(TObject self)
    {
        return PyBoolObject.True;
    }

    protected internal virtual PyResult Int(TObject self)
    {
        // TOOD: is this implementation correct?
        var index = Index(self);
        if (index.IsError)
            return index;

        if (!PySpecialMethods.TryGetIndex(index.Value, out var i))
            return PyResult.CaptureExceptionFromPVM();

        return i;
    }
    protected internal virtual PyResult Float(TObject self)
    {
        // TOOD: is this implementation correct?
        var index = Index(self);
        if (index.IsError)
            return index;

        if (!PySpecialMethods.TryGetIndex(index.Value, out var i))
            return PyResult.CaptureExceptionFromPVM();

        return PyFloatObject.FromDouble((double)i.Value);
    }
    protected internal virtual PyResult Complex(TObject self)
    {
        // TOOD: is this implementation correct?
        var index = Index(self);
        if (index.IsError)
            return index;

        if (!PySpecialMethods.TryGetIndex(index.Value, out var i))
            return PyResult.CaptureExceptionFromPVM();

        throw new NotImplementedException();
    }

    protected internal virtual PyResult Index(TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Contains(TObject self, PyObject item)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult GetItem(TObject self, PyObject item)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult SetItem(TObject self, PyObject key, PyObject value)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult DelItem(TObject self, PyObject key)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Len(TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Iter(TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Next(TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Neg(TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Pos(TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Invert(TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Abs(TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Add(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Sub(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Mul(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult TrueDiv(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult FloorDiv(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Mod(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult DivMod(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Pow(TObject self, PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult LShift(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RShift(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult And(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Xor(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Or(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyResult RAdd(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RSub(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RMul(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RTrueDiv(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RFloorDiv(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RMod(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RDivMod(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RPow(TObject self, PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RLShift(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RRShift(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RAnd(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RXor(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult ROr(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyResult Lt(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Le(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Eq(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Ne(TObject self, PyObject other)
    {
        var eq = Eq(self, other);
        if (eq.IsError)
            return eq;

        if (PySpecialMethods.TryGetBool(eq.Value, out var b))
            return b.BoolValue ? PyBoolObject.False : PyBoolObject.True;

        return PyResult.CaptureExceptionFromPVM();
    }
    protected internal virtual PyResult Gt(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Ge(TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyResult Missing(TObject self, PyObject key)
    {
        return PyResult.RaiseKeyError(key);
    }

    protected internal virtual PyResult Get(TObject self, PyObject instance, PyObject owner)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyResult Set(TObject self, PyObject instance, PyObject value)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyResult Delete(TObject self, PyObject instance)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyResult SetName(TObject self, PyObject owner, PyObject name)
    {
        return PyNoneObject.None;
    }

    protected internal virtual PyResult Format(TObject self, string formatSpec)
    {
        if (formatSpec.Length is 0)
            return Str(self);

        return PyResult.RaiseTypeError($"unsupported format string passed to {Name}.__format__");
    }

}
