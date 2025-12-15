using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyModules.Builtins;

public partial class PyObject : IEquatable<PyObject>
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

    private int? _pyId;
    internal ConcurrentDictionary<string, PyObject>? _pyMembers;

    public virtual PyTypeObject PyType => PyBuiltinTypes.Object;
    public int PyId => _pyId ??= Interlocked.Increment(ref _pyNextId);

    internal IDictionary<string, PyObject> PyAttributes => _pyMembers ??= [];

    public PyObject()
    {
    }

    internal virtual PyObject GetActualInstanceForCallDescriptor(PyTypeObject baseType)
    {
        // this method is used to support CustomObject._backingObjects
        return this;
    }

    internal static bool PyObjectHasAttribute(PyObject pyObj, string name)
    {
        if (pyObj.PyAttributes.ContainsKey(name))
            return true;

        foreach (var pyType in pyObj.PyType.MRO)
        {
            if (pyType.PyAttributes.ContainsKey(name))
                return true;
        }

        if (name is PySpecialNames.Class)
            return true;

        return false;
    }

    internal static PyObject? PyObjectGetAttribute(PyObject pyObj, string name)
    {
        PyObject? attrFromType = null;
        PyTypeObject? ownerType = null; // not null if attrFromType is not null
        foreach (var pyType in pyObj.PyType.MRO)
        {
            if (pyType.PyAttributes.TryGetValue(name, out attrFromType))
            {
                ownerType = pyType;
                break;
            }
        }

        PyObject? nonDataDescriptor = null;
        if (attrFromType is not null && Utils.IsDescriptor(attrFromType, out var hasGet, out var hasSet, out var hasDelete))
        {
            if (hasGet)
            {
                if (hasSet || hasDelete)
                {
                    Debug.Assert(ownerType is not null);
                    pyObj = pyObj.GetActualInstanceForCallDescriptor(ownerType);
                    return attrFromType.Get(pyObj, pyObj.PyType);
                }

                nonDataDescriptor = attrFromType;
            }
        }

        if (pyObj.PyAttributes.TryGetValue(name, out var attr))
            return attr;

        if (nonDataDescriptor is not null)
        {
            Debug.Assert(ownerType is not null);
            pyObj = pyObj.GetActualInstanceForCallDescriptor(ownerType);
            return nonDataDescriptor.Get(pyObj, pyObj.PyType);
        }

        if (attrFromType is not null)
            return attrFromType;

        // special read-only attributes
        // __class__
        if (name is PySpecialNames.Class)
            return pyObj.PyType;

        return PyVirtualMachine.RaiseAttributeError($"'{pyObj.PyType.Name}' object has no attribute '{name}'");
    }

    internal static PyObject? PyObjectSetAttribute(PyObject pyObj, string name, PyObject value)
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

    internal static PyObject? PyObjectDeleteAttribute(PyObject pyObj, string name)
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

    public PyObjectType()
    {
        AppendSpecialMethodsAsDescriptorsDirectly<PyObject>(
            nameof(Repr), nameof(Str), nameof(Bool), nameof(Hash),
            nameof(Eq), nameof(Ne), nameof(Lt), nameof(Le), nameof(Gt), nameof(Ge),
            nameof(GetAttribute), nameof(SetAttr), nameof(DelAttr),
            nameof(Init)
        );
    }

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateEmpty())
        {
            return PyVirtualMachine.RaiseTypeError(null);
        }

        return new PyObject();
    }
}