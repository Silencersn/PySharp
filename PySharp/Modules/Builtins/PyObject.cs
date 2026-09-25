using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

public partial class PyObject
{
    internal PyTypeObject? _pyType;

    public PyTypeObject PyType => _pyType ?? DefaultPyType;
    public virtual PyTypeObject DefaultPyType => PyObjectType.Shared;
    public long PyId => PyAttachedPropertiesManager.Shared.GetId(this);

    internal virtual IPyAttributesObject PyAttributes
    {
        get
        {
            if (IsImmutable)
                return IPyAttributesObject.FrozenEmpty;

            return PyAttachedPropertiesManager.Shared.GetDict(this);
        }
        set
        {
            if (IsImmutable)
                throw new NotSupportedException();

            PyAttachedPropertiesManager.Shared.SetDict(this, value);
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsImmutable => PyType.InstancesAreImmutable;

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
        // object.ToString is a fixed-signature .NET face; a user __repr__
        // runs on the ambient context while a run is active and on the
        // CSharpRuntime sentinel when there is no execution to attach to
        var result = PySpecialMethods.Repr(PyCallContext.Current ?? PyCallContext.CSharpRuntime, this);
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
        // CPython exposes object.__setattr__/__delattr__ through wrap_setattr/
        // wrap_delattr, whose hackcheck refuses every type-object target; the
        // generic setattro slot stays hack-free so `cls.x = v` (type_setattro)
        // keeps working
        PyAttributes[PySpecialNames.SetAttr] = new PyWrapperDescriptorObject((PyTernaryFunction)HackCheckedSetAttr);
        PyAttributes[PySpecialNames.DelAttr] = new PyWrapperDescriptorObject((PyBinaryFunction)HackCheckedDelAttr);
        FillSlot(PySpecialNames.Init, ref Slots.Init, DefaultInit);
    }

    private static PyResult HackCheckedSetAttr(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        if (self is PyTypeObject)
            return PyResult.TypeError(PySR.Runtime_Object_CannotApplySetAttr, self.PyType.TpName);

        return DefaultSetAttr(context, self, key, value);
    }

    private static PyResult HackCheckedDelAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is PyTypeObject)
            return PyResult.TypeError(PySR.Runtime_Object_CannotApplyDelAttr, self.PyType.TpName);

        return DefaultDelAttr(context, self, item);
    }

    /// <summary>
    /// Implements object.__init_subclass__.
    /// In CPython this is a no-op classmethod (METH_CLASS | METH_NOARGS): it
    /// terminates the cooperative __init_subclass__ chain and rejects every
    /// argument, which is what makes an unconsumed class keyword an error.
    /// The rejection names the class the descriptor bound to (methodobject.c
    /// meth_get__qualname__).
    /// </summary>
    [PyClassMethod(PySpecialNames.InitSubclass)]
    [PyFunctionParameters("**kwargs")]
    private static PyResult InitSubclassImpl(PyCallContext context, PyTypeObject cls, PyArguments arguments)
    {
        if (arguments.ExtraKwargs.Count is not 0)
        {
            return PyResult.TypeError(
                PySR.Runtime_Exception_TakesNoKeywordArguments,
                $"{cls.QualName}.{PySpecialNames.InitSubclass}");
        }

        return PyNoneObject.None;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython object_new: excess arguments are an error unless a custom
        // __init__ (with the default __new__) is there to receive them;
        // inherited slot delegates are shared by reference, so reference
        // equality detects the defaults
        if (args.Count is not 0 || kwargs.Count is not 0)
        {
            if (!ReferenceEquals(cls.Slots.New, Slots.New))
                return PyResult.TypeError(PySR.Runtime_Object_NewTakesExactlyOneArg);
            if (ReferenceEquals(cls.Slots.Init, Slots.Init))
                return PyResult.TypeError(PySR.Runtime_Object_TakesNoArguments, cls.Name);
        }

        if (cls.LayoutType == typeof(PyObjectManagedDict))
            return new PyObjectManagedDict { _pyType = cls };

        return new PyObject { _pyType = cls };
    }
}

public partial class PyObjectManagedDict : PyObject
{
    private protected IPyAttributesObject? _pyAttributes;

    // Objects with a real per-instance dict are mutable (CPython: these types
    // have a non-zero tp_dictoffset, so instances expose a __dict__).
    internal override bool IsImmutable => false;

    internal override IPyAttributesObject PyAttributes
    {
        get => _pyAttributes ??= new PyDictObject();
        set => _pyAttributes = value;
    }
}