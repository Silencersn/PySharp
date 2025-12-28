using PySharp.PyRuntime.Calls;
using System.Diagnostics;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject
{
    protected internal virtual PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Init(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Call(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Repr(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Str(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Hash(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult GetAttribute(PyCallContext context, PyObject self, string item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult GetAttr(PyCallContext context, PyObject self, string item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult SetAttr(PyCallContext context, PyObject self, string key, PyObject value)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult DelAttr(PyCallContext context, PyObject self, string item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Bool(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Int(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Float(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Complex(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Index(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Contains(PyCallContext context, PyObject self, PyObject item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult GetItem(PyCallContext context, PyObject self, PyObject item)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult SetItem(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult DelItem(PyCallContext context, PyObject self, PyObject key)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Len(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Iter(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Next(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Neg(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Pos(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Invert(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Abs(PyCallContext context, PyObject self)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Add(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Sub(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Mul(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult TrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult FloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Mod(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult DivMod(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Pow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult LShift(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RShift(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult And(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Xor(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Or(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult RAdd(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RSub(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RMul(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RTrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RFloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RMod(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RDivMod(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RPow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RLShift(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RRShift(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RAnd(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult RXor(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult ROr(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Lt(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Le(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Eq(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Ne(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Gt(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
    protected internal virtual PyResult Ge(PyCallContext context, PyObject self, PyObject other)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Missing(PyCallContext context, PyObject self, PyObject key)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Get(PyCallContext context, PyObject self, PyObject instance, PyObject owner)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Set(PyCallContext context, PyObject self, PyObject instance, PyObject value)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Delete(PyCallContext context, PyObject self, PyObject instance)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult SetName(PyCallContext context, PyObject self, PyObject owner, PyObject name)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }

    protected internal virtual PyResult Format(PyCallContext context, PyObject self, string formatSpec)
    {
        throw new UnreachableException("Implemented by PyTypeObject<TObject>");
    }
}
