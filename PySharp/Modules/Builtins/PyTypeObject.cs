using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

public abstract partial class PyTypeObject : PyObjectManagedDict, IPyObjectName
{
    private PyTypeObject[] _mro;

    // CPython tp_subclasses: a weak registry of the direct subclasses, walked
    // when a dunder assignment/deletion on this type must re-resolve inherited
    // slots down the subtree (typeobject.c update_subclasses). The entries are
    // weak on purpose — a strong list held by the base would pin every
    // runtime-created class forever.
    private readonly Lock _subclassLock = new();
    private List<WeakReference<PyTypeObject>>? _subclasses;

    private void RegisterSubclass(PyTypeObject subclass)
    {
        // only runtime-created bases are ever walked: the type-attribute
        // guards reject writes on static types, so their registries would be
        // unreachable ballast (hundreds of startup-time entries in object's)
        if (!IsRuntimeCreated)
            return;

        lock (_subclassLock)
        {
            _subclasses ??= [];
            // nothing removes an entry at type death, so prune collected
            // subclasses while registering — types that never trigger a slot
            // update would otherwise grow their bases' registries unbounded
            _subclasses.RemoveAll(static weakRef => !weakRef.TryGetTarget(out _));
            _subclasses.Add(new WeakReference<PyTypeObject>(subclass));
        }
    }

    // type_set_bases removes the type from every base it is leaving
    // (remove_all_subclasses); symmetric with RegisterSubclass, including
    // the pruning of collected entries
    private void UnregisterSubclass(PyTypeObject subclass)
    {
        lock (_subclassLock)
        {
            _subclasses?.RemoveAll(weakRef =>
                !weakRef.TryGetTarget(out var target) || ReferenceEquals(target, subclass));
        }
    }

    private List<PyTypeObject> EnumerateLiveSubclasses()
    {
        lock (_subclassLock)
        {
            if (_subclasses is null)
                return [];

            // the registry is the only holder of these weak references, so
            // collected subclasses are pruned while walking
            _subclasses.RemoveAll(static weakRef => !weakRef.TryGetTarget(out _));
            var live = new List<PyTypeObject>(_subclasses.Count);
            foreach (var weakRef in _subclasses)
            {
                if (weakRef.TryGetTarget(out var subclass))
                    live.Add(subclass);
            }
            return live;
        }
    }

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

    // CPython tp_dictoffset != 0: whether instances carry a real per-instance
    // dict — the __slots__/__dict__ plumbing keys off this rather than the
    // type-attribute guard that InstancesAreImmutable also models
    internal virtual bool InstancesCarryInstanceDict => !InstancesAreImmutable;

    // CPython Py_TPFLAGS_HEAPTYPE: only runtime-created classes accept
    // attribute writes on the type object itself
    internal virtual bool IsRuntimeCreated => false;

    // CPython tp_free's allocation regime: the freelist types (int, str,
    // float, bytes, complex, bytearray) release with PyObject_Free while
    // GC-tracked containers and heap classes use PyObject_GC_Del, so a
    // __bases__/__class__ walk between the two reports a deallocator
    // difference (compatible_for_assignment, Objects/typeobject.c:7063)
    internal virtual bool ReleasesWithFreeList => false;

    /// <summary>
    /// The modeled CPython tp_flags bits (sequence/mapping pattern matching,
    /// match-self). Resolved once during construction: static bits from the
    /// table for builtins, then inherit_patma_flags along the MRO.
    /// </summary>
    internal PyTypeFlags TypeFlags { get; private set; }

    private void ResolveTypeFlags()
    {
        var flags = IsRuntimeCreated ? PyTypeFlags.None : PyTypeFlagsTable.Lookup(TpName);
        // CPython inherit_patma_flags (typeobject.c:8713): walk the MRO and
        // take a base's bits only while the type has none of its own
        for (int i = 1; i < InternalMRO.Length; i++)
        {
            var baseFlags = InternalMRO[i].TypeFlags;
            if ((flags & PyTypeFlags.CollectionMask) == 0)
                flags |= baseFlags & PyTypeFlags.CollectionMask;
            if ((flags & PyTypeFlags.MatchSelf) == 0)
                flags |= baseFlags & PyTypeFlags.MatchSelf;
        }
        TypeFlags = flags;
    }

    internal PyTypeObject()
    {
        if (DefaultModule is not null)
            ModuleAsObject = PyStrObject.FromString(DefaultModule);
        Name = DefaultName;
        QualName = DefaultQualName;
        var bases = Bases;
        _mro = [this, .. CreateMROWithoutSelf(bases)];
        Slots = PyTypeSlots.Create(MRO.Skip(1));
        ResolveTypeFlags();
        foreach (var baseType in bases.Distinct())
            baseType.RegisterSubclass(this);
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
        ResolveTypeFlags();
        foreach (var baseType in bases.Distinct())
            baseType.RegisterSubclass(this);
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

    internal abstract PyTypeObject CreateUserDefinedTypeWithSameLayout(string name, string qualName, IReadOnlyList<PyTypeObject> bases, bool excludesInstanceDict);

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

    // solid_base (Objects/typeobject.c): the strongest layout along the
    // base chain. A plain heap class shares object's payload — its layout
    // type is the shared managed-dict wrapper — so it never anchors and
    // the walk continues through its bases.
    internal static PyTypeObject SolidBaseOf(PyTypeObject type)
    {
        while (!AnchorsLayout(type) && type.Bases.Count > 0)
            type = StrongestLayoutOf(type.Bases);
        return type;
    }

    // a type whose layout type is the shared managed-dict wrapper carries
    // object's payload and leaves the anchoring to a base; the dict-less
    // slots sibling grows the payload (like any slotted layout) and anchors
    private static bool AnchorsLayout(PyTypeObject type)
        => type.LayoutType != typeof(PyObjectManagedDict);

    // best_base's layout race: the first base whose payload type derives
    // from every other candidate's
    private static PyTypeObject StrongestLayoutOf(IEnumerable<PyTypeObject> bases)
    {
        var owner = PyObjectType.Shared;
        foreach (var baseType in bases)
        {
            if (baseType.LayoutType != owner.LayoutType && baseType.LayoutType.IsSubclassOf(owner.LayoutType))
                owner = baseType;
        }
        return owner;
    }

    // type_set_bases rewrites the bases of a heap type in place; the base
    // class's Bases is the shared [object] sentinel, so only a derived
    // type with a writable list can land here
    internal virtual void OverwriteBases(IReadOnlyList<PyTypeObject> bases)
        => throw new UnreachableException();

    // the commit tail of type_set_bases (update_all_slots walks the same
    // rebuilt hierarchy): swap in the new linearization and re-derive the
    // slots and flags cached from the old chain
    private void RebuildMro(List<PyTypeObject> baseLinearization)
    {
        _mro = [this, .. baseLinearization];
        Slots = PyTypeSlots.Create(MRO.Skip(1));
        ResolveTypeFlags();
    }

    // mro_hierarchy_for_complete_type's hierarchy walk: the registered
    // direct subclasses of each runtime-created type, breadth first so a
    // class is always recomputed after its ancestors
    private List<PyTypeObject> CollectSubtree()
    {
        var tree = new List<PyTypeObject> { this };
        var seen = new HashSet<PyTypeObject> { this };
        for (int i = 0; i < tree.Count; i++)
        {
            foreach (var subclass in tree[i].EnumerateLiveSubclasses())
            {
                if (seen.Add(subclass))
                    tree.Add(subclass);
            }
        }
        return tree;
    }

    // type_set_bases_unlocked (Objects/typeobject.c:1791): validate the
    // replacement bases, recompute the MRO of the whole subtree, and only
    // then commit bases, registries and MROs — a failure anywhere leaves
    // the graph untouched
    internal static PyResult ApplyBasesAssignment(PyTypeObject self, PyObject value)
    {
        // check_set_special_type_attr's immutable guard ran in the setter

        if (value is not PyTupleObject tuple)
            return PyResult.TypeError(PySR.Runtime_Type_BasesNotTuple, self.TpName, value.PyType.Name);

        if (tuple.Count is 0)
            return PyResult.TypeError(PySR.Runtime_Type_BasesEmpty, self.TpName);

        var newBases = new List<PyTypeObject>(tuple.Count);
        foreach (var element in tuple)
        {
            if (element is not PyTypeObject baseType)
                return PyResult.TypeError(PySR.Runtime_Type_BasesNonClass, self.TpName, element.PyType.Name);

            // is_subtype_with_mro: a base that is already a descendant of
            // the type would close an inheritance cycle
            if (baseType.IsSubclassOf(self))
                return PyResult.TypeError(PySR.Runtime_Type_BasesCycle);

            newBases.Add(baseType);
        }

        // best_base's BASETYPE gate
        foreach (var baseType in newBases)
        {
            if (baseType.IsSealed)
                return PyResult.TypeError(PySR.Runtime_Inheritance_UnacceptableBaseType, baseType.Name);
        }

        // compatible_for_assignment (Objects/typeobject.c:7059), at the
        // level of the chosen base and the old tp_base — the level whose
        // names the message reports. The allocation regime is compared
        // first (tp_free), then the solid bases must still agree.
        var oldOwner = StrongestLayoutOf(self.Bases);
        var newOwner = StrongestLayoutOf(newBases);
        if (!ReferenceEquals(newOwner, oldOwner))
        {
            if (newOwner.ReleasesWithFreeList != oldOwner.ReleasesWithFreeList)
                return PyResult.TypeError(PySR.Runtime_Type_BasesDeallocatorDiffers, newOwner.TpName, oldOwner.TpName);

            if (!ReferenceEquals(SolidBaseOf(newOwner), SolidBaseOf(oldOwner)))
                return PyResult.TypeError(PySR.Runtime_Type_BasesLayoutDiffers, newOwner.TpName, oldOwner.TpName);
        }

        var tree = self.CollectSubtree();
        var overriddenMros = new Dictionary<PyTypeObject, PyTypeObject[]>();
        var newLinearizations = new List<(PyTypeObject Type, List<PyTypeObject> BaseLinearization)>(tree.Count);
        foreach (var type in tree)
        {
            var bases = ReferenceEquals(type, self) ? newBases : type.Bases;
            // the duplicate sanity check of mro_implementation_unlocked,
            // which only runs when several bases compete
            if (bases.Count > 1)
            {
                var seenBases = new HashSet<PyTypeObject>();
                foreach (var baseType in bases)
                {
                    if (!seenBases.Add(baseType))
                        return PyResult.TypeError(PySR.Runtime_Inheritance_DuplicateBase, baseType.Name);
                }
            }

            if (!TryCreateMROWithoutSelf(bases, overriddenMros, out var linearization, out var stuckHeads))
                return PyResult.TypeError(PySR.Runtime_Inheritance_CannotCreateMRO, string.Join(", ", stuckHeads.Select(head => head.Name)));

            overriddenMros[type] = [type, .. linearization];
            newLinearizations.Add((type, linearization));
        }

        // leave the old bases, enter the new ones, then rewrite every MRO
        foreach (var oldBase in self.Bases.Distinct())
            oldBase.UnregisterSubclass(self);
        self.OverwriteBases(newBases);
        foreach (var newBase in newBases.Distinct())
            newBase.RegisterSubclass(self);
        foreach (var (type, linearization) in newLinearizations)
            type.RebuildMro(linearization);

        return PyNoneObject.None;
    }

    private static bool TryCreateMROWithoutSelf(IEnumerable<PyTypeObject> bases, [NotNullWhen(true)] out List<PyTypeObject>? mro, [NotNullWhen(false)] out List<PyTypeObject>? stuckHeads)
    {
        return TryCreateMROWithoutSelf(bases, overriddenMros: null, out mro, out stuckHeads);
    }

    private static bool TryCreateMROWithoutSelf(IEnumerable<PyTypeObject> bases, IReadOnlyDictionary<PyTypeObject, PyTypeObject[]>? overriddenMros, [NotNullWhen(true)] out List<PyTypeObject>? mro, [NotNullWhen(false)] out List<PyTypeObject>? stuckHeads)
    {
        // L[C(B1 ... BN)] = C + merge(L[B1] ... L[BN], B1 ... BN)
        mro = [];
        stuckHeads = [];

        // B1 ... BN
        var baseTypes = new Queue<PyTypeObject>(bases);
        if (baseTypes.Count is 0)
            // the type of object
            return true;

        // L[B1] ... L[BN] — an ancestor recomputed during a __bases__
        // assignment provides its not-yet-committed linearization
        List<Queue<PyTypeObject>> baseMros = [.. baseTypes.Select(baseType => new Queue<PyTypeObject>(
            overriddenMros is not null && overriddenMros.TryGetValue(baseType, out var overriddenMro)
                ? overriddenMro
                : baseType.MRO))];

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
