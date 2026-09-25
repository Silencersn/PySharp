using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

public abstract partial class PyTypeObject : PyObjectManagedDict, IPyObjectName
{
    private readonly PyTypeObject[] _mro;

    public virtual IReadOnlyList<PyTypeObject> Bases => [PyObjectType.Shared];
    internal ReadOnlySpan<PyTypeObject> InternalMRO => _mro;
    public IReadOnlyList<PyTypeObject> MRO => _mro;
    protected virtual string? DefaultModule => "builtins";
    public string? Module =>
        ModuleAsObject is PyStrObject str ? str.Value :
        ModuleAsObject is not null ? "<unknown>" : null;
    public PyObject? ModuleAsObject { get; internal set; }

    protected abstract string DefaultName { get; }
    public string Name { get; internal set; }

    // The display rule shared by object_repr, type_repr and GenericAlias
    // rendering (Objects/typeobject.c:6924 and 2303, Objects/typevarobject.c:262):
    // a type whose __module__ is a string other than "builtins" shows as
    // "module.qualname" (__main__ included), anything else falls back to the
    // plain tp_name.
    public string ReprName
    {
        get
        {
            if (ModuleAsObject is PyStrObject { Value: var module } && module is not "builtins")
                return $"{module}.{QualName}";
            return Name;
        }
    }

    protected virtual string DefaultQualName => DefaultName;
    public string QualName { get; internal set; }

    // CPython tp_name, which is what nearly every error message names a type by
    // (Objects/object.c:1313 and 2001, Objects/call.c:194, Objects/abstract.c:203
    // and 1699). type_new_set_name (Objects/typeobject.c:4233) sets it to the
    // bare __name__ for a class created at runtime, whatever its __qualname__ or
    // __module__ say, while a declared type keeps the name it was registered
    // with — module prefix included, as PyType_FromMetaclass-derived extension
    // types do (Objects/typeobject.c:5092).
    public string TpName => IsRuntimeCreated ? Name : QualName;

    // CPython's %T, the fully qualified name read by
    // _PyType_GetFullyQualifiedName (Objects/typeobject.c:1589): a declared type
    // is named by its tp_name, a runtime-created class as "module.qualname" with
    // "builtins" and "__main__" dropped. Used by the dict-key/set-element hash
    // failure wording and by complex() — the places CPython reaches for %T
    // rather than tp_name.
    public string FullyQualifiedName
    {
        get
        {
            if (!IsRuntimeCreated)
                return TpName;
            if (ModuleAsObject is PyStrObject { Value: var module }
                && module is not "builtins" and not PySpecialNames.Main)
                return $"{module}.{QualName}";
            return QualName;
        }
    }

    public virtual bool IsSealed => false;
    public override PyTypeObject DefaultPyType => PyTypeObjectType.Shared;
    public abstract Type LayoutType { get; }
    /// <summary>
    /// Whether instances of this type are immutable: an immutable instance carries no
    /// per-instance <c>__dict__</c> and rejects attribute writes and deletions
    /// (CPython types with a zero tp_dictoffset, such as int, str and tuple), which is
    /// also why the type-attribute guards refuse writes on their type objects.
    /// </summary>
    internal virtual bool InstancesAreImmutable => true;

    // CPython Py_TPFLAGS_HEAPTYPE: only runtime-created classes accept
    // attribute writes on the type object itself
    internal virtual bool IsRuntimeCreated => false;

    internal PyTypeObject()
    {
        if (DefaultModule is not null)
            ModuleAsObject = PyStrObject.FromString(DefaultModule);
        Name = DefaultName;
        QualName = DefaultQualName;
        _mro = [this, .. CreateMROWithoutSelf(Bases)];
        Slots = PyTypeSlots.Create(MRO.Skip(1));
        PyAttributes[PySpecialNames.Doc] = PyNoneObject.None;
    }

    internal PyTypeObject(string qualName, IReadOnlyList<PyTypeObject> bases)
    {
        if (DefaultModule is not null)
            ModuleAsObject = PyStrObject.FromString(DefaultModule);
        Name = qualName.Split('.').Last();
        QualName = qualName;
        _mro = [this, .. CreateMROWithoutSelf(bases)];
        Slots = PyTypeSlots.Create(MRO.Skip(1));
        PyAttributes[PySpecialNames.Doc] = PyNoneObject.None;
    }

    public bool IsInstance(PyObject obj)
    {
        return obj.PyType.IsSubclassOf(this);
    }

    public bool IsSubclassOf(PyTypeObject pyType)
    {
        // CPython recursive_issubclass compares by identity — a metaclass
        // __eq__ never participates here, so no user code can run
        foreach (var baseType in InternalMRO)
        {
            if (ReferenceEquals(baseType, pyType))
                return true;
        }

        return false;
    }

    internal abstract PyTypeObject CreateUserDefinedTypeWithSameLayout(string name, string qualName, IReadOnlyList<PyTypeObject> bases);

    internal static PyResult<PyTypeObject> ValidateBasesAndResolveLayoutTypeOwner(IEnumerable<PyTypeObject> bases)
    {
        // CPython best_base vets each base (BASETYPE flag, then layout)
        // before the MRO step's duplicate scan, so a sealed base must win
        // over "duplicate base class" in combined-error cases
        var layoutTypeOwner = PyObjectType.Shared;
        foreach (var baseType in bases)
        {
            if (baseType.IsSealed)
                return PyResult.TypeError(PySR.Runtime_Inheritance_UnacceptableBaseType, baseType.Name);

            // best_base keeps the winner when the base's layout is the same
            // or an ancestor of it (a redundant base like object after a
            // derived layout owner); only unrelated layouts conflict
            if (baseType.LayoutType.IsSubclassOf(layoutTypeOwner.LayoutType))
                layoutTypeOwner = baseType;
            else if (!baseType.LayoutType.IsAssignableFrom(layoutTypeOwner.LayoutType))
                return PyResult.TypeError(PySR.Runtime_Inheritance_LayoutConflict);
        }

        var seenBases = new HashSet<PyTypeObject>();
        foreach (var baseType in bases)
        {
            if (!seenBases.Add(baseType))
                return PyResult.TypeError(PySR.Runtime_Inheritance_DuplicateBase, baseType.Name);
        }

        if (!TryCreateMROWithoutSelf(bases, out _, out var stuckHeads))
            return PyResult.TypeError(PySR.Runtime_Inheritance_CannotCreateMRO, string.Join(", ", stuckHeads.Select(stuckHead => stuckHead.Name)));

        return layoutTypeOwner;
    }

    private static bool TryCreateMROWithoutSelf(IEnumerable<PyTypeObject> bases, [NotNullWhen(true)] out List<PyTypeObject>? mro, [NotNullWhen(false)] out List<PyTypeObject>? stuckHeads)
    {
        // L[C(B1 ... BN)] = C + merge(L[B1] ... L[BN], B1 ... BN)
        mro = [];
        stuckHeads = [];

        // B1 ... BN
        var baseTypes = new Queue<PyTypeObject>(bases);
        if (baseTypes.Count is 0)
            // the type of object
            return true;

        // L[B1] ... L[BN]
        List<Queue<PyTypeObject>> baseMros = [.. baseTypes.Select(baseType => new Queue<PyTypeObject>(baseType.MRO))];

        // L[B1] ... L[BN], B1 ... BN
        baseMros.Add(baseTypes);

        while (baseMros.Count > 0)
        {
            // take the head of the first list, i.e L[B1][0];
            // if this head is not in the tail of any of the other lists,
            // then add it to the linearization of C and remove it from the lists in the merge,
            // otherwise look at the head of the next list and take it, if it is a good head.
            //
            // Then repeat the operation until all the class are removed or it is impossible to find good heads.
            // In this case, it is impossible to construct the merge,
            // it will refuse to create the class C and will raise an exception.
            //
            for (int i = 0; i < baseMros.Count; i++)
            {
                var head = baseMros[i].Peek();
                bool notInOtherTails = true;
                for (int j = 0; j < baseMros.Count; j++)
                {
                    if (i == j)
                        continue;

                    var tail = baseMros[j].Skip(1);
                    if (tail.Contains(head))
                    {
                        notInOtherTails = false;
                        break;
                    }
                }
                if (notInOtherTails)
                {
                    mro.Add(head);
                    List<Queue<PyTypeObject>> baseMrosToRemove = [];
                    foreach (var baseMro in baseMros)
                    {
                        if (ReferenceEquals(baseMro.Peek(), head))
                        {
                            baseMro.Dequeue();
                            if (baseMro.Count is 0)
                                baseMrosToRemove.Add(baseMro);
                        }
                    }
                    foreach (var baseMroToRemove in baseMrosToRemove)
                    {
                        var removed = baseMros.Remove(baseMroToRemove);
                        Debug.Assert(removed);
                    }
                    break;
                }
                else if (i == baseMros.Count - 1)
                {
                    mro = null;
                    // set_mro_error: the merge is blocked by the distinct
                    // current heads of the remaining lists
                    var seenHeads = new HashSet<PyTypeObject>();
                    foreach (var baseMro in baseMros)
                    {
                        if (seenHeads.Add(baseMro.Peek()))
                            stuckHeads.Add(baseMro.Peek());
                    }
                    return false;
                }
            }
        }

        return true;
    }


    private static List<PyTypeObject> CreateMROWithoutSelf(IEnumerable<PyTypeObject> bases)
    {
        if (TryCreateMROWithoutSelf(bases, out var mro, out _))
            return mro;

        throw new UnreachableException();
    }
}
