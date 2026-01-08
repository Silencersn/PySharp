using PySharp.AstNodes;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject<TObject>
{
    protected virtual PyResult Init(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyNoneObject.None;
    }

    protected virtual PyResult Call(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Repr(PyCallContext context, TObject self)
    {
        return DefaultRepr(context, self);
    }

    protected virtual PyResult Str(PyCallContext context, TObject self)
    {
        return DefaultStr(context, self);
    }

    protected virtual PyResult Hash(PyCallContext context, TObject self)
    {
        return DefaultHash(context, self);
    }

    protected virtual PyResult GetAttribute(PyCallContext context, TObject self, PyObject item)
    {
        return DefaultGetAttribute(context, self, item);
    }

    protected virtual PyResult GetAttr(PyCallContext context, TObject self, PyObject item)
    {
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{item}'");
    }

    protected virtual PyResult SetAttr(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        if (self.IsImmutable)
            return PyResult.RaiseTypeError($"cannot set '{key}' attribute of immutable type '{Name}'");

        return DefaultSetAttr(context, self, key, value);
    }

    protected virtual PyResult DelAttr(PyCallContext context, TObject self, PyObject item)
    {
        if (self.IsImmutable)
            return PyResult.RaiseTypeError($"cannot set '{item}' attribute of immutable type '{Name}'");

        return DefaultDelAttr(context, self, item);
    }

    protected virtual PyResult Bool(PyCallContext context, TObject self)
    {
        return DefaultBool(context, self);
    }

    protected virtual PyResult Int(PyCallContext context, TObject self)
    {
        // TOOD: is this implementation correct?
        var index = PySpecialMethods.Index(context, self);
        if (index.IsError)
            return index;

        return PySpecialMethods.Index(context, index.Value);
    }
    protected virtual PyResult Float(PyCallContext context, TObject self)
    {
        // TOOD: is this implementation correct?
        var index = PySpecialMethods.Index(context, self);
        if (index.IsError)
            return index;

        var result = PySpecialMethods.Index(context, index.Value);
        if (result.IsError)
            return result;

        return PyFloatObject.FromDouble((double)result.Value.Value);
    }
    protected virtual PyResult Complex(PyCallContext context, TObject self)
    {
        // TOOD: is this implementation correct?
        var index = PySpecialMethods.Index(context, self);
        if (index.IsError)
            return index;

        var result = PySpecialMethods.Index(context, index.Value);
        if (result.IsError)
            return result;

        throw new NotImplementedException();
    }

    protected virtual PyResult Index(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Contains(PyCallContext context, TObject self, PyObject item)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult GetItem(PyCallContext context, TObject self, PyObject item)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult SetItem(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult DelItem(PyCallContext context, TObject self, PyObject key)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Len(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Iter(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Next(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Neg(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Pos(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Invert(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Abs(PyCallContext context, TObject self)
    {
        return PyResult.RaiseTypeError(null);
    }

    protected virtual PyResult Add(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Sub(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Mul(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult TrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult FloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Mod(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult DivMod(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Pow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult LShift(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RShift(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult And(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Xor(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Or(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected virtual PyResult RAdd(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RSub(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RMul(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RTrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RMod(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RDivMod(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RLShift(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RRShift(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RAnd(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult RXor(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult ROr(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected virtual PyResult Lt(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Le(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Eq(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Ne(PyCallContext context, TObject self, PyObject other)
    {
        // TODO: call PyOperators.Ne
        var eq = PyOperators.Eq(context, self, other);
        if (eq.IsError || eq.IsNotImplemented)
            return eq;

        var result = PySpecialMethods.Bool(context, eq.Value).PyUnwrap(context);
        return result.BoolValue ? PyBoolObject.False : PyBoolObject.True;
    }
    protected virtual PyResult Gt(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    protected virtual PyResult Ge(PyCallContext context, TObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }

    protected virtual PyResult Missing(PyCallContext context, TObject self, PyObject key)
    {
        return PyResult.RaiseKeyError(key);
    }

    protected virtual PyResult Get(PyCallContext context, TObject self, PyObject instance, PyObject owner)
    {
        throw new NotImplementedException();
    }

    protected virtual PyResult Set(PyCallContext context, TObject self, PyObject instance, PyObject value)
    {
        throw new NotImplementedException();
    }

    protected virtual PyResult Delete(PyCallContext context, TObject self, PyObject instance)
    {
        throw new NotImplementedException();
    }

    protected virtual PyResult SetName(PyCallContext context, TObject self, PyObject owner, PyObject name)
    {
        return PyNoneObject.None;
    }

    protected virtual PyResult Format(PyCallContext context, TObject self, PyObject formatSpec)
    {
        return DefaultFormat(context, self, formatSpec);
    }

}
