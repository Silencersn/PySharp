using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

public partial class PyObject
{
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
}

[PyType("object")]
public sealed partial class PyObjectType : PyTypeObject<PyObjectType, PyObject>
{
    internal static readonly PyBinaryFunction GenericGetAttribute = DefaultGetAttribute;

    public override IReadOnlyList<PyTypeObject> Bases => [];

    public PyObjectType()
    {
        FillSlot(PySpecialNames.Repr, ref Slots.Repr, DefaultRepr);
        FillSlot(PySpecialNames.Str, ref Slots.Str, DefaultStr);
        FillSlot(PySpecialNames.Bool, ref Slots.Bool, DefaultBool);
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

        void FillSlot<TDelegate>(string name, ref TDelegate? field, TDelegate func) where TDelegate : Delegate
        {
            field = func;
            PyAttributes.Add(name, new PyWrapperDescriptorObject(func));
        }
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (ReferenceEquals(cls, this) /* Do we need to consider an externally created PyObjectType? */
            && (args.Count is not 0 || kwargs.Count is not 0))
            return PyResult.TypeError(PySR.Runtime_Object_NewTakesExactlyOneArg);

        return new PyObject { _pyType = cls };
    }
}