using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PySharp.Modules.Builtins;

public partial class PyObject
{
    private static ConditionalWeakTable<PyObject, IDictionary<string, PyObject>> GlobalDictManager { get; } = [];
    private static ConditionalWeakTable<PyObject, object> GlobalIdManager { get; } = [];

    private static long _pyNextId = 0;

    internal PyTypeObject? _pyType;

    public PyTypeObject PyType => _pyType ?? DefaultPyType;
    public virtual PyTypeObject DefaultPyType => PyObjectType.Shared;
    public long PyId => (long)GlobalIdManager.GetOrAdd(this, static _ => Interlocked.Increment(ref _pyNextId));

    internal virtual IDictionary<string, PyObject> PyAttributes
    {
        get
        {
            if (IsImmutable)
                return FrozenDictionary<string, PyObject>.Empty;

            return GlobalDictManager.GetOrAdd(this, static _ => new ConcurrentDictionary<string, PyObject>());
        }
        set
        {
            if (IsImmutable)
                throw new NotSupportedException();

            GlobalDictManager.AddOrUpdate(this, value);
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsImmutable => PyType.IsTypeImmutable;

    public PyObject()
    {
    }

    internal static bool TryLookupAttrInMro(PyTypeObject pyType, string name, [NotNullWhen(true)] out PyObject? attr)
    {
        foreach (var baseType in pyType.InternalMRO)
        {
            if (baseType.PyAttributes.TryGetValue(name, out attr))
                return true;
        }

        attr = null;
        return false;
    }

    internal static bool PyObjectHasAttribute(PyObject pyObj, string name)
    {
        if (pyObj.PyAttributes.ContainsKey(name))
            return true;

        foreach (var pyType in pyObj.PyType.InternalMRO)
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
}

[PyType("object")]
[PyTypeConstructor(DoNotGenerateConstructor = true)]
public sealed partial class PyObjectType : PyTypeObject<PyObject>
{
    internal static readonly PyBinaryFunction GenericGetAttribute = DefaultGetAttribute;
    public static PyTypeObject Shared { get; } = new PyObjectType();


    public override IReadOnlyList<PyTypeObject> Bases => [];

    private PyObjectType()
    {
        Slots.Number = new();

        FillSlot(PySpecialNames.Repr, ref Slots.Repr, DefaultRepr);
        FillSlot(PySpecialNames.Str, ref Slots.Str, DefaultStr);
        FillSlot(PySpecialNames.Bool, ref Slots.Number.Bool, DefaultBool);
        FillSlot(PySpecialNames.Hash, ref Slots.Hash, DefaultHash);
        FillSlot(PySpecialNames.Eq, ref Slots.Eq, DefaultEq);
        FillSlot(PySpecialNames.Ne, ref Slots.Ne, DefaultNe);
        FillSlot(PySpecialNames.Lt, ref Slots.Lt, DefaultBinaryOperator);
        FillSlot(PySpecialNames.Le, ref Slots.Le, DefaultBinaryOperator);
        FillSlot(PySpecialNames.Gt, ref Slots.Gt, DefaultBinaryOperator);
        FillSlot(PySpecialNames.Ge, ref Slots.Ge, DefaultBinaryOperator);
        FillSlot(PySpecialNames.GetAttribute, ref Slots.GetAttribute, GenericGetAttribute);
        FillSlot(PySpecialNames.SetAttr, ref Slots.SetAttr, DefaultSetAttr);
        FillSlot(PySpecialNames.DelAttr, ref Slots.DelAttr, DefaultDelAttr);
        FillSlot(PySpecialNames.Init, ref Slots.Init, DefaultInit);
    }

    /// <summary>
    /// Implements object.__init_subclass__.
    /// In CPython this is a no-op classmethod (METH_CLASS | METH_NOARGS).
    /// It provides a terminal node for the cooperative __init_subclass__ chain.
    /// Currently accepts **kwargs silently for simplicity.
    /// </summary>
    [PyClassMethod(PySpecialNames.InitSubclass)]
    [PyFunctionParameters("**kwargs")]
    private static PyResult InitSubclassImpl(PyCallContext context, PyTypeObject cls, PyArguments arguments)
    {
        return PyNoneObject.None;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (ReferenceEquals(cls, this) && (args.Count is not 0 || kwargs.Count is not 0))
            return PyResult.TypeError(PySR.Runtime_Object_NewTakesExactlyOneArg);

        if (cls.LayoutType == typeof(PyObjectManagedDict))
            return new PyObjectManagedDict { _pyType = cls };

        return new PyObject { _pyType = cls };
    }
}

public partial class PyObjectManagedDict : PyObject
{
    internal IDictionary<string, PyObject>? _pyAttributes;

    internal override IDictionary<string, PyObject> PyAttributes
    {
        get => _pyAttributes ??= new ConcurrentDictionary<string, PyObject>();
        set => _pyAttributes = value;
    }
}