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

            var eq = PyOperators.Eq(PyCallContext.CSharpRuntime, x, y);
            if (eq.IsError)
            {
                throw new PyRuntimeException(eq.Exception);
            }
            else if (eq.IsNotImplemented)
            {
                return x.PyId == y.PyId;
            }

            if (eq.Value is PyBoolObject boolObj)
                return boolObj.BoolValue;

            var result = PySpecialMethods.Bool(PyCallContext.CSharpRuntime, eq.Value);
            if (result.IsSuccessful)
                return result.Value.BoolValue;

            throw new PyRuntimeException(result.Exception);
        }

        public int GetHashCode([DisallowNull] PyObject obj)
        {
            var result = PySpecialMethods.Hash(PyCallContext.CSharpRuntime, obj);
            if (result.IsSuccessful)
                return result.Value.Int32Value;

            throw new PyRuntimeException(result.Exception);
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

            var lt = PyOperators.Lt(PyCallContext.CSharpRuntime, x, y);
            if (lt.IsError)
                throw new PyRuntimeException(lt.Exception);

            var result = PySpecialMethods.Bool(PyCallContext.CSharpRuntime, lt.Value);
            if (result.IsError)
                throw new PyRuntimeException(result.Exception);

            return result.Value.BoolValue ? -1 : 1;
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
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsImmutable => PyType.IsTypeImmutable;

    public PyObject()
    {
    }

    internal static bool TryLookupAttrInMro(PyTypeObject pyType, string name, [NotNullWhen(true)] out PyObject? attr)
    {
        foreach (var baseType in pyType.MRO)
        {
            if (baseType.PyAttributes.TryGetValue(name, out attr))
                return true;
        }

        attr = null;
        return false;
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

    public override string ToString()
    {
        var result = PySpecialMethods.Repr(PyCallContext.CSharpRuntime, this);
        if (result.IsSuccessful)
            return $"{GetType().Name}{{id={PyId},repr={result.Value.Value}}}";
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
        var result = PySpecialMethods.Hash(PyCallContext.CSharpRuntime, this);
        if (result.IsSuccessful)
            return result.Value.UncheckedInt32Value is -1 ? -2 : result.Value.UncheckedInt32Value;

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

public sealed class PyObjectType : PyTypeObject<PyObjectType, PyObject>
{
    public override string Name => "object";
    public override IReadOnlyList<PyTypeObject> Bases => [];

    public PyObjectType()
    {
        AppendSpecialMethodDescriptors(nameof(Repr), nameof(Str), nameof(Bool), nameof(Hash),
            nameof(Eq), nameof(Ne), nameof(Lt), nameof(Le), nameof(Gt), nameof(Ge),
            nameof(GetAttribute), nameof(SetAttr), nameof(DelAttr),
            nameof(Init));
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (ReferenceEquals(cls, this) /* Do we need to consider an externally created PyObjectType? */
            && (args.Count is not 0 || kwargs.Count is not 0))
            return PyResult.RaiseTypeError("object.__new__() takes exactly one argument (the type to instantiate)");

        return new PyObject { _pyType = cls };
    }
}