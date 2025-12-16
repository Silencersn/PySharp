using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.Utility;
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

    internal int? _pyId;
    internal IDictionary<string, PyObject>? _pyAttributes;
    internal PyTypeObject? _pyType;

    public PyTypeObject PyType => _pyType ?? DefaultPyType;
    public virtual PyTypeObject DefaultPyType => PyObjectType.Shared;
    public int PyId => _pyId ??= Interlocked.Increment(ref _pyNextId);

    internal IDictionary<string, PyObject> PyAttributes => _pyAttributes ??= new ConcurrentDictionary<string, PyObject>();
    internal bool IsSelfDefaultType => ReferenceEquals(PyType, DefaultPyType);
    internal virtual bool IsImmutable => PyType.IsImmutable;

    public PyObject()
    {
    }

    internal static bool PyObjectHasAttribute(PyObject pyObj, string name)
    {
        if (pyObj._pyAttributes is not null && pyObj._pyAttributes.ContainsKey(name))
            return true;

        foreach (var pyType in pyObj.PyType.MRO)
        {
            // check pyObj while not check pyType
            // because pyType._pyAttributes is always not null
            if (pyType.PyAttributes.ContainsKey(name))
                return true;
        }

        if (name is PySpecialNames.Class)
            return true;

        return false;
    }

    private static bool TryFindAttrFromMRO(PyObject pyObj, string name, [NotNullWhen(true)] out PyObject? attrFromType, [NotNullWhen(true)] out PyTypeObject? ownerType)
    {
        foreach (var pyType in pyObj.PyType.MRO)
        {
            if (pyType.PyAttributes.TryGetValue(name, out attrFromType))
            {
                ownerType = pyType;
                return true;
            }
        }

        attrFromType = null;
        ownerType = null;
        return false;
    }

    internal static PyObject? PyObjectGetAttribute(PyObject pyObj, string name)
    {
        PyObject? nonDataDescriptor = null;
        if (TryFindAttrFromMRO(pyObj, name, out var attrFromType, out _) &&
            Utils.IsDescriptor(attrFromType, out var hasGet, out var hasSet, out var hasDelete))
        {
            if (hasGet)
            {
                if (hasSet || hasDelete)
                    return attrFromType.GetImpl(pyObj, pyObj.PyType);

                nonDataDescriptor = attrFromType;
            }
        }

        if (pyObj._pyAttributes is not null && pyObj._pyAttributes.TryGetValue(name, out var attr))
            return attr;

        if (nonDataDescriptor is not null)
            return nonDataDescriptor.GetImpl(pyObj, pyObj.PyType);

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
        if (TryFindAttrFromMRO(pyObj, name, out var attrFromType, out _) &&
            Utils.IsDescriptor(attrFromType, out _, out var hasSet, out _))
        {
            if (hasSet)
                return attrFromType.SetImpl(pyObj, value);
        }

        pyObj.PyAttributes[name] = value;
        return PyNoneObject.None;
    }

    internal static PyObject? PyObjectDeleteAttribute(PyObject pyObj, string name)
    {
        if (TryFindAttrFromMRO(pyObj, name, out var attrFromType, out _) &&
            Utils.IsDescriptor(attrFromType, out _, out _, out var hasDelete))
        {
            if (hasDelete)
                return attrFromType.DeleteImpl(pyObj);
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

public sealed class PyObjectType : PyPrimitiveTypeObject<PyObjectType, PyObject>
{
    public override string Name => "object";
    public override IReadOnlyList<PyTypeObject> Bases => [];

    public PyObjectType()
    {
        AppendSpecialMethodsAsDescriptorsDirectly<PyObject>(
            nameof(ReprImpl), nameof(StrImpl), nameof(BoolImpl), nameof(HashImpl),
            nameof(EqImpl), nameof(NeImpl), nameof(LtImpl), nameof(LeImpl), nameof(GtImpl), nameof(GeImpl),
            nameof(GetAttributeImpl), nameof(SetAttrImpl), nameof(DelAttrImpl),
            nameof(InitImpl)
        );
    }

    protected internal override PyObject? NewImpl(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is not 0 || kwargs.Count is not 0)
            return PyVirtualMachine.RaiseTypeError("object.__new__() takes exactly one argument (the type to instantiate)");

        return new PyObject();
    }
}