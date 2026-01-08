using PySharp.PyRuntime.Calls;
using System.Diagnostics;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject
{
    protected virtual PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Init(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Call(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Repr(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Str(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Hash(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult GetAttribute(PyCallContext context, PyObject self, PyObject item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult GetAttr(PyCallContext context, PyObject self, PyObject item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult SetAttr(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult DelAttr(PyCallContext context, PyObject self, PyObject item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Bool(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Int(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Float(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Complex(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Index(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Contains(PyCallContext context, PyObject self, PyObject item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult GetItem(PyCallContext context, PyObject self, PyObject item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult SetItem(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult DelItem(PyCallContext context, PyObject self, PyObject key)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Len(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Iter(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Next(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Neg(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Pos(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Invert(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Abs(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Add(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Sub(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Mul(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult TrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult FloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Mod(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult DivMod(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Pow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult LShift(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RShift(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult And(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Xor(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Or(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult RAdd(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RSub(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RMul(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RTrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RFloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RMod(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RDivMod(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RPow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RLShift(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RRShift(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RAnd(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult RXor(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult ROr(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Lt(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Le(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Eq(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Ne(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Gt(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    private protected virtual PyResult Ge(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Missing(PyCallContext context, PyObject self, PyObject key)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Get(PyCallContext context, PyObject self, PyObject instance, PyObject owner)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Set(PyCallContext context, PyObject self, PyObject instance, PyObject value)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Delete(PyCallContext context, PyObject self, PyObject instance)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult SetName(PyCallContext context, PyObject self, PyObject owner, PyObject name)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    private protected virtual PyResult Format(PyCallContext context, PyObject self, PyObject formatSpec)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
}
