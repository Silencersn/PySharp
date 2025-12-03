using PySharp.PyRuntime;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace PySharp.PyObjects.Builtins;

public class PyObject : IEquatable<PyObject>
{
    private protected class PyObjectRuntimeEqualityComparer : IEqualityComparer<PyObject>
    {
        internal static readonly PyObjectRuntimeEqualityComparer Shared = new();

        public bool Equals(PyObject? x, PyObject? y)
        {
            if (x is null)
                return y is null;

            if (y is null)
                return false;

            var eq = PyOperators.Eq(x, y);
            if (eq is null)
            {
                Debug.Assert(PyVirtualMachine.CurrentException is not null);
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }
            else if (eq is PyNotImplementedObject)
            {
                return x.PyId == y.PyId;
            }

            if (PySpecialMethods.TryGetBool(eq, out var b))
                return b.BoolValue;

            Debug.Assert(PyVirtualMachine.CurrentException is not null);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        public int GetHashCode([DisallowNull] PyObject obj)
        {
            if (PySpecialMethods.TryGetHash(obj, out var hash))
                return hash.Int32Value;

            Debug.Assert(PyVirtualMachine.CurrentException is not null);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }
    }

    private protected class PyObjectRuntimeComparer : IComparer<PyObject>
    {
        internal static readonly PyObjectRuntimeComparer Shared = new();

        public int Compare(PyObject? x, PyObject? y)
        {
            if (PyObjectRuntimeEqualityComparer.Shared.Equals(x, y))
                return 0;

            if (x is null)
                return -1;

            if (y is null)
                return 1;

            if (!PyInteropService.TryGetLt(x, y, out var result))
            {
                Debug.Assert(PyVirtualMachine.CurrentException is not null);
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }

            return result ? -1 : 1;
        }
    }

    private static int _pyNextId = 0;

    private readonly int _pyId;
    internal ConcurrentDictionary<string, PyObject>? _pyMembers;

    public virtual PyTypeObject PyType => PyBuiltinTypes.Object;
    public int PyId => _pyId;

    internal IDictionary<string, PyObject> PyAttributes => _pyMembers ??= [];

    public PyObject()
    {
        _pyId = Interlocked.Increment(ref _pyNextId);
    }

    public static PyObject? PyObjectGetAttribute(PyObject pyObj, string name)
    {
        PyObject? attrFromType = null;
        foreach (var pyType in pyObj.PyType.MRO)
        {
            if (pyType.PyAttributes.TryGetValue(name, out attrFromType))
                break;
        }

        PyObject? nonDataDescriptor = null;
        if (attrFromType is not null && Utils.IsDescriptor(attrFromType, out var hasGet, out var hasSet, out var hasDelete))
        {
            if (hasGet)
            {
                if (hasSet || hasDelete)
                    return attrFromType.Get(pyObj, pyObj.PyType);

                nonDataDescriptor = attrFromType;
            }
        }

        if (pyObj.PyAttributes.TryGetValue(name, out var attr))
            return attr;

        if (nonDataDescriptor is not null)
            return nonDataDescriptor.Get(pyObj, pyObj.PyType);

        if (attrFromType is not null)
            return attrFromType;

        return PyVirtualMachine.RaiseAttributeError($"'{pyObj.PyType.Name}' object has no attribute '{name}'");
    }

    public static PyObject? PyObjectSetAttribute(PyObject pyObj, string name, PyObject value)
    {
        PyObject? attrFromType = null;
        foreach (var pyType in pyObj.PyType.MRO)
        {
            if (pyType.PyAttributes.TryGetValue(name, out attrFromType))
                break;
        }

        if (attrFromType is not null && Utils.IsDescriptor(attrFromType, out var hasGet, out var hasSet, out var hasDelete))
        {
            if (hasSet)
                return attrFromType.Set(pyObj, value);
        }

        pyObj.PyAttributes[name] = value;
        return PyNoneObject.None;
    }

    public static PyObject? PyObjectDeleteAttribute(PyObject pyObj, string name)
    {
        PyObject? attrFromType = null;
        foreach (var pyType in pyObj.PyType.MRO)
        {
            if (pyType.PyAttributes.TryGetValue(name, out attrFromType))
                break;
        }

        if (attrFromType is not null && Utils.IsDescriptor(attrFromType, out var hasGet, out var hasSet, out var hasDelete))
        {
            if (hasSet)
                return attrFromType.Delete(pyObj);
        }

        var removed = pyObj.PyAttributes.Remove(name);
        if (!removed)
            return PyVirtualMachine.RaiseAttributeError($"'{pyObj.PyType.Name}' object has no attribute '{name}'");

        return PyNoneObject.None;
    }

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
        return new PyStrObject($"<{PyType.Name} object at {PyId:X16}>");
    }

    public virtual PyObject? Str()
    {
        return Repr();
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

    public override string ToString()
    {
        if (PySpecialMethods.TryGetRepr(this, out var s))
            return $"{GetType().Name}{{id={PyId},repr={s.Value}}}";
        return $"{GetType().Name}{{id={PyId}}}";
    }

    public bool Equals(PyObject? other)
    {
        return PyObjectRuntimeEqualityComparer.Shared.Equals(this, other);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not PyObject pyObj)
            return false;

        return Equals(pyObj);
    }

    public override int GetHashCode()
    {
        if (PySpecialMethods.TryGetHash(this, out var hash))
            return hash.Int32Value;

        PyVirtualMachine.ClearException();
        return PyId.GetHashCode();
    }

    public static bool operator ==(PyObject? left, PyObject? right)
    {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(PyObject? left, PyObject? right)
    {
        return !(left == right);
    }
}

public sealed class PyObjectType : PyTypeObject
{
    public override string Name => "object";
    public override IReadOnlyList<PyTypeObject> Bases => [];

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
        {
            return PyVirtualMachine.RaiseTypeError(null);
        }

        return new PyObject();
    }
}