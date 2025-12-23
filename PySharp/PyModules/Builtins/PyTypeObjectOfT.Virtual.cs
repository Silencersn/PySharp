using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject<TObject>
{
    protected internal virtual PyResult Init(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyNoneObject.None;
    }

    protected internal virtual PyResult Call(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Repr(PyCallContext context, TObject self)
    {
        return PyStrObject.FromString($"<{Name} object at 0x{self.PyId:X16}>");
    }

    protected internal virtual PyResult Str(PyCallContext context, TObject self)
    {
        return self.PyType.Repr(context, self);
    }

    protected internal virtual PyResult Hash(PyCallContext context, TObject self)
    {
        return PyIntObject.FromInteger(self.PyId);
    }

    protected internal virtual PyResult GetAttribute(PyCallContext context, TObject self, string item)
    {
        return PyObjectGetAttribute(context, self, item);
    }

    protected internal virtual PyResult GetAttr(PyCallContext context, TObject self, string item)
    {
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{item}'");
    }

    protected internal virtual PyResult SetAttr(PyCallContext context, TObject self, string key, PyObject value)
    {
        if (self.IsImmutable)
            return PyResult.RaiseTypeError($"cannot set '{key}' attribute of immutable type '{Name}'");

        return PyObjectSetAttribute(context, self, key, value);
    }

    protected internal virtual PyResult DelAttr(PyCallContext context, TObject self, string item)
    {
        if (self.IsImmutable)
            return PyResult.RaiseTypeError($"cannot set '{item}' attribute of immutable type '{Name}'");

        return PyObjectDeleteAttribute(context, self, item);
    }

    protected internal virtual PyResult Bool(PyCallContext context, TObject self)
    {
        return PyBoolObject.True;
    }

    protected internal virtual PyResult Int(PyCallContext context, TObject self)
    {
        // TOOD: is this implementation correct?
        var index = self.PyType.Index(context, self);
        if (index.IsError)
            return index;

        if (!PySpecialMethods.TryGetIndex(index.Value, out var i))
            return PyResult.CaptureExceptionFromPVM();

        return i;
    }
    protected internal virtual PyResult Float(PyCallContext context, TObject self)
    {
        // TOOD: is this implementation correct?
        var index = self.PyType.Index(context, self);
        if (index.IsError)
            return index;

        if (!PySpecialMethods.TryGetIndex(index.Value, out var i))
            return PyResult.CaptureExceptionFromPVM();

        return PyFloatObject.FromDouble((double)i.Value);
    }
    protected internal virtual PyResult Complex(PyCallContext context, TObject self)
    {
        // TOOD: is this implementation correct?
        var index = self.PyType.Index(context, self);
        if (index.IsError)
            return index;

        if (!PySpecialMethods.TryGetIndex(index.Value, out var i))
            return PyResult.CaptureExceptionFromPVM();

        throw new NotImplementedException();
    }

    protected internal virtual PyResult Index(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Contains(PyCallContext context, TObject self, PyObject item)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult GetItem(PyCallContext context, TObject self, PyObject item)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult SetItem(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult DelItem(PyCallContext context, TObject self, PyObject key)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Len(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Iter(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Next(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Neg(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Pos(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Invert(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Abs(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected internal virtual PyResult Add(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Sub(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Mul(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult TrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult FloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Mod(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult DivMod(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Pow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult LShift(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RShift(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult And(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Xor(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Or(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyResult RAdd(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RSub(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RMul(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RTrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RMod(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RDivMod(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RLShift(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RRShift(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RAnd(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult RXor(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult ROr(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyResult Lt(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Le(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Eq(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Ne(PyCallContext context, TObject self, PyObject other)
    {
        var eq = Eq(context, self, other);
        if (eq.IsError)
            return eq;

        if (PySpecialMethods.TryGetBool(eq.Value, out var b))
            return b.BoolValue ? PyBoolObject.False : PyBoolObject.True;

        return PyResult.CaptureExceptionFromPVM();
    }
    protected internal virtual PyResult Gt(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected internal virtual PyResult Ge(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected internal virtual PyResult Missing(PyCallContext context, TObject self, PyObject key)
    {
        return PyResult.RaiseKeyError(key);
    }

    protected internal virtual PyResult Get(PyCallContext context, TObject self, PyObject instance, PyObject owner)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyResult Set(PyCallContext context, TObject self, PyObject instance, PyObject value)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyResult Delete(PyCallContext context, TObject self, PyObject instance)
    {
        throw new NotImplementedException();
    }

    protected internal virtual PyResult SetName(PyCallContext context, TObject self, PyObject owner, PyObject name)
    {
        return PyNoneObject.None;
    }

    protected internal virtual PyResult Format(PyCallContext context, TObject self, string formatSpec)
    {
        if (formatSpec.Length is 0)
            return self.PyType.Str(context, self);

        return PyResult.RaiseTypeError($"unsupported format string passed to {Name}.__format__");
    }

}
