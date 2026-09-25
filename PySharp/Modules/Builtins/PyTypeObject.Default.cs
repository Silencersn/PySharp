using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject
{
    internal static PyResult DefaultRepr(PyCallContext context, PyObject self)
    {
        return PyStrObject.FromString($"<{self.PyType.ReprName} object at 0x{self.PyId:X16}>");
    }
    internal static PyResult DefaultStr(PyCallContext context, PyObject self)
    {
        return PySpecialMethods.Repr(context, self);
    }
    internal static PyResult DefaultBool(PyCallContext context, PyObject self)
    {
        // CPython PyObject_IsTrue: without __bool__, a type defining __len__
        // uses its length; plain objects default to true.
        if (self.PyType.Slots.Len is not null)
        {
            var len = PySpecialMethods.Len(context, self);
            if (len.IsError)
                return len;
            return PyBoolObject.FromBoolean(len.Value.Value != 0);
        }
        return PyBoolObject.True;
    }
    internal static PyResult DefaultHash(PyCallContext context, PyObject self)
    {
        return PyIntObject.FromInteger(self.GetHashCode());
    }

    // CPython PyObject_HashNotImplemented: __hash__ = None marks a type
    // explicitly unhashable and blocks inheritance of object's identity hash
    internal static PyResult HashNotImplemented(PyCallContext context, PyObject self)
    {
        return PyResult.TypeError(PySR.Runtime_Object_Unhashable, self.PyType.Name);
    }

    // __hash__ slot wiring treats None as the explicit unhashable marker
    // instead of wrapping it as a callable
    internal static void SetHashSlot(PyTypeSlots slots, PyObject value)
    {
        if (value is PyNoneObject)
            slots.Hash = HashNotImplemented;
        else
            slots.TrySetSlot(PySpecialNames.Hash, value);
    }
    internal static PyResult DefaultGetAttribute(PyCallContext context, PyObject self, PyObject item)
    {
        // if this method changed,
        // change PyCore.GetAttrOrMethod together

        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.TpName);

        var type = self.PyType;
        var name = str.Value;

        if (name is PySpecialNames.Class)
            return type;

        if (name is PySpecialNames.Dict)
        {
            // Objects without a genuine instance dict (built-in values, builtin
            // functions, methods, code, ...) have no __dict__ (CPython).
            if (self.IsImmutable)
                return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.TpName, name);
            return self.PyAttributes.Self;
        }

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            if (PyUtils.IsDataDescriptor(attr))
            {
                var getFunc = attr.PyType.Slots.Get;
                if (getFunc is not null)
                    return getFunc(context, attr, self, type);
            }
        }

        if (self.PyAttributes.TryGetValue(name, out var value) is true)
            return value;

        if (attr is not null)
        {
            var getFunc = attr.PyType.Slots.Get;
            if (getFunc is not null)
                return getFunc(context, attr, self, type);

            return attr;
        }

        return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.TpName, name);
    }

    internal static PyResult DefaultSetAttr(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        if (key is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, key.PyType.TpName);

        var type = self.PyType;
        var name = str.Value;

        // CPython type_setattro: the immutable-type check fires before any
        // descriptor or instance-dict handling
        if (self is PyTypeObject ownerType && !ownerType.IsRuntimeCreated)
            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, name, ownerType.TpName);

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            var func = attr.PyType.Slots.Set;
            if (func is not null)
                return func(context, attr, self, value);
        }

        // CPython reaches __class__ through the getset descriptor on object,
        // which the MRO lookup above would have found first when a class body
        // shadows the name: only an unshadowed __class__ is the type pointer
        if (name is PySpecialNames.Class && attr is null)
            return SetClassAttribute(self, value);

        // CPython _PyObject_GenericSetAttrWithDict: dict-less instances
        // (IsImmutable) reject the write before any instance-dict handling
        if (self.IsImmutable)
            return FrozenAttrWriteError(type, name, attr);

        // CPython subtype_setdict -> _PyObject_SetDict: the value replaces the
        // whole instance dict (PyDict_Check accepts a subclass), and a type
        // object reads __dict__ through a getset with no setter
        if (name is PySpecialNames.Dict)
        {
            if (self is PyTypeObject)
                return PyResult.AttributeError(PySR.Runtime_Type_DictNotWritable);

            if (value is not PyDictObject assigned)
                return PyResult.TypeError(PySR.Runtime_Object_DictMustBeDictionary, value.PyType.Name);

            self.PyAttributes = assigned;
            return PyNoneObject.None;
        }

        self.PyAttributes[name] = value;

        // Setting a dunder on a type object re-resolves the slot here and on
        // every subclass whose dict does not shadow it (CPython update_slot).
        // Assigning object's default __new__/__init__ back participates too:
        // UpdateOneSlot re-wires object's own delegate so a previous override
        // stops applying, exactly like CPython rewiring tp_new/tp_init
        if (self is PyTypeObject typeObj)
            UpdateSlot(typeObj, name);

        return PyNoneObject.None;
    }

    // CPython _PyObject_GenericSetAttrWithDict: dict-less instances reject
    // attribute writes with two AttributeError shapes — a name found in the
    // MRO without a setter is "read-only", anything else is the shared
    // "no attribute and no __dict__" error (assignment and deletion alike)
    private static PyResult FrozenAttrWriteError(PyTypeObject type, string name, PyObject? attr)
    {
        if (attr is not null)
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeReadOnly, type.TpName, name);

        return PyResult.AttributeError(PySR.Runtime_Object_AttributeNoDict, type.TpName, name);
    }

    // CPython object_set_class: the value must be a class, both sides must be
    // mutable (ModuleType subclasses qualify) and share a layout, and only the
    // type pointer moves — __init__ never runs and the instance dict stays
    private static PyResult SetClassAttribute(PyObject self, PyObject value)
    {
        if (value is not PyTypeObject newType)
            return PyResult.TypeError(PySR.Runtime_Object_ClassMustBeClass, value.PyType.Name);

        var oldType = self.PyType;

        if (!IsModuleSubclass(oldType) || !IsModuleSubclass(newType))
        {
            if (!oldType.IsRuntimeCreated || !newType.IsRuntimeCreated)
                return PyResult.TypeError(PySR.Runtime_Object_ClassAssignmentNotMutable);
        }

        if (newType.LayoutType != oldType.LayoutType)
            return PyResult.TypeError(PySR.Runtime_Object_ClassLayoutDiffers, newType.Name, oldType.Name);

        self._pyType = newType;
        return PyNoneObject.None;
    }

    private static bool IsModuleSubclass(PyTypeObject type) => type.IsSubclassOf(PyModuleObjectType.Shared);

    // CPython type_update_dict: identity against object's dict entry detects
    // that a __new__/__init__ value IS object's default, letting callers keep
    // creation-time FillNullWith wiring instead of converting a closure — a
    // converted closure would lose the ReferenceEquals default probes.
    // object's MRO is always [object], so this is an O(1) dict read. CPython
    // keeps no slot provenance metadata either: tp_dict is the single source
    // of truth and update_one_slot re-derives the specific test per call
    internal static bool IsObjectDefaultSlotValue(string name, PyObject value)
    {
        if (name is not (PySpecialNames.New or PySpecialNames.Init))
            return false;

        return PyObjectType.Shared.PyAttributes.TryGetValue(name, out var defaultValue)
            && ReferenceEquals(value, defaultValue);
    }

    // CPython fixup_slot_dispatchers generalized to every slot (typeobject.c
    // update_one_slot): each slot re-resolves through the first MRO entry
    // defining the dunder in its OWN dict. The eager FillNullWith pass bakes
    // inherited copies of an ancestor's delegate into base slots, and such a
    // copy then masks a later base's real method (a non-first parent's
    // __ne__/__gt__/__ge__/__hash__ was unreachable — the original 9-name
    // fixup; the same masking exists for every other family, e.g. __iter__),
    // so creation-time resolution walks the MRO dicts for all names.
    internal static void FixupAllSlots(PyTypeObject type)
    {
        foreach (var name in PyTypeSlots.AllSlotNames)
            UpdateOneSlot(type, name, wireOwn: false);
    }

    // CPython update_slot + update_subclasses: after a dunder dict entry on a
    // runtime class is written or deleted, re-resolve the slot on the type and
    // recursively on every registered subclass whose own dict does not shadow
    // the name — this is what makes a later Base.__add__ = g (or its deletion)
    // visible to classes that already inherited the slot.
    internal static void UpdateSlot(PyTypeObject type, string name)
    {
        if (!PyTypeSlots.IsSlotName(name))
            return;

        UpdateOneSlot(type, name, wireOwn: true);
        foreach (var subclass in type.EnumerateLiveSubclasses())
        {
            // CPython recurse_down_subclasses: a subclass whose own dict
            // defines the name shields its whole subtree from this change
            if (subclass.PyAttributes.ContainsKey(name))
                continue;
            UpdateSlot(subclass, name);
        }
    }

    private static void UpdateOneSlot(PyTypeObject type, string name, bool wireOwn)
    {
        var mro = type.InternalMRO;
        for (int i = 0; i < mro.Length; i++)
        {
            var entry = mro[i];
            if (!entry.PyAttributes.TryGetValue(name, out var value))
                continue;

            if (i is 0)
            {
                // The type's own dict entry is already wired (namespace scan at
                // creation, setattr for later mutations) — creation-time
                // resolution keeps that exact delegate: rewiring would lose
                // FillNewSlot's validation closure and the reflected-synthesis
                // delegates. The mutation entry point repeats the previous
                // setattr sync; __hash__ treats None as the unhashable marker.
                // One exception at creation: an own object-default
                // __new__/__init__ entry is skipped by the namespace scan
                // (IsObjectDefaultSlotValue gates it out), so without rewiring
                // the slot would keep FillNullWith's baked ANCESTOR delegate
                // when an ancestor overrides — CPython's specific test wires
                // object's tp_new/tp_init for that idiom too
                if (!wireOwn && !IsObjectDefaultSlotValue(name, value))
                    return;

                if (name is PySpecialNames.Hash)
                {
                    SetHashSlot(type.Slots, value);
                }
                else if (name is PySpecialNames.New or PySpecialNames.Init)
                {
                    // object's default in the own dict re-wires object's own
                    // delegate (CPython rewires tp_new/tp_init), so an
                    // overriding ancestor and a previous override stop applying
                    if (IsObjectDefaultSlotValue(name, value))
                    {
                        if (name is PySpecialNames.New)
                            type.Slots.New = PyObjectType.Shared.Slots.New;
                        else
                            type.Slots.Init = PyObjectType.Shared.Slots.Init;
                    }
                    else
                    {
                        // known pre-existing divergence: CPython's tp_new
                        // special case (typeobject.c:11362-11383) installs
                        // `specific = (void *)type->tp_new`, i.e. it KEEPS
                        // the existing tp_new when a native type's __new__
                        // wrapper (dict.__new__ & co) is assigned; here the
                        // value is converted and the cross-type call fails
                        // on the type check
                        type.Slots.TrySetSlot(name, value);
                    }
                }
                else
                {
                    type.Slots.TrySetSlot(name, value);
                }
                return;
            }

            // __new__/__init__ resolve to the provider's own slot delegate so
            // FillNewSlot's cls-validation wrapper and object's ReferenceEquals
            // default probes keep working — a converted closure over the dict
            // value would lose both. A null provider slot never happens: every
            // dict-carried provider also owns the slot (FillSlot wiring) and
            // object sits at the end of every MRO with both slots filled
            if (name is PySpecialNames.New or PySpecialNames.Init)
            {
                // creation-time resolution keeps the FillNullWith wiring when
                // the provider is object's default — the slot already holds
                // object's delegate. A mutation must still re-wire: after
                // del cls.__init__/__new__ the slot carries the deleted
                // override's delegate, and CPython update_one_slot re-resolves
                // it to object's tp_init/tp_new
                if (!wireOwn && IsObjectDefaultSlotValue(name, value))
                    return;

                if (name is PySpecialNames.New)
                {
                    if (entry.Slots.New is { } newFunc)
                        type.Slots.New = newFunc;
                }
                else if (entry.Slots.Init is { } initFunc)
                {
                    type.Slots.Init = initFunc;
                }
                return;
            }

            // __hash__ = None marks a type explicitly unhashable regardless of
            // which ancestor dict carries the None (dict's read face)
            if (name is PySpecialNames.Hash && value is PyNoneObject)
            {
                type.Slots.Hash = HashNotImplemented;
                return;
            }

            // object's dict entries for __setattr__/__delattr__ are the
            // hackchecked wrappers, a different delegate from the hack-free
            // generic setattro slot. CPython's hackcheck wraps exactly
            // object_setattr so its specific test still hits; here the entry
            // resolves back to object's hack-free slot delegate instead —
            // never to a stale converted closure left by an earlier override
            if ((name is PySpecialNames.SetAttr or PySpecialNames.DelAttr)
                && ReferenceEquals(entry, PyObjectType.Shared))
            {
                if (name is PySpecialNames.SetAttr)
                    type.Slots.SetAttr = PyObjectType.Shared.Slots.SetAttr;
                else
                    type.Slots.DelAttr = PyObjectType.Shared.Slots.DelAttr;
                return;
            }

            // specific: a wrapper descriptor carries the provider's exact slot
            // delegate (FillSlot shares the instance between the slot field and
            // the type dict), so re-resolving an inherited native method keeps
            // the direct call without a closure
            if (value is PyWrapperDescriptorObject wrapper && type.Slots.TrySetWrappedSlot(name, wrapper))
                return;

            type.Slots.TrySetSlot(name, value);
            return;
        }

        // CPython update_one_slot: no dict provider anywhere clears the slot
        type.Slots.ClearSlot(name);
    }

    internal static PyResult DefaultDelAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.TpName);

        var type = self.PyType;
        var name = str.Value;

        // same immutable-type gate as the set path; the delete path reports
        // "cannot set" in CPython too
        if (self is PyTypeObject ownerType && !ownerType.IsRuntimeCreated)
            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, name, ownerType.TpName);

        // __doc__ has no metaclass descriptor (reads resolve through the MRO
        // like a plain attribute), so its guard lives here: CPython refuses
        // the delete on every type object, heap types included
        if (self is PyTypeObject docType && name is PySpecialNames.Doc)
            return PyResult.TypeError($"cannot delete '{PySpecialNames.Doc}' attribute of immutable type '{docType.Name}'");

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            var func = attr.PyType.Slots.Delete;
            if (func is not null)
                return func(context, attr, self);

            // a data descriptor without __delete__ cannot be deleted; the
            // error points at the missing hook, not at the attribute
            if (PyUtils.IsDataDescriptor(attr))
                return PyResult.AttributeError(PySR.Runtime_Attribute_NoDelete);
        }

        // CPython object_set_class rejects the NULL value up front, so an
        // unshadowed __class__ is never deletable
        if (name is PySpecialNames.Class && attr is null)
            return PyResult.TypeError(PySR.Runtime_Object_ClassCannotDelete);

        // same dict-less gate as the set path; CPython reports the same two
        // AttributeError shapes for deletion too
        if (self.IsImmutable)
            return FrozenAttrWriteError(type, name, attr);

        // CPython subtype_setdict with a NULL value drops the dict: the next
        // read materializes an empty one, the dropped dict keeps its items
        if (name is PySpecialNames.Dict)
        {
            if (self is PyTypeObject)
                return PyResult.AttributeError(PySR.Runtime_Type_DictNotWritable);

            self.PyAttributes = new PyDictObject();
            return PyNoneObject.None;
        }

        var removed = self.PyAttributes.Remove(name);
        if (!removed)
        {
            if (self is PyTypeObject heapType)
                return PyResult.AttributeError(PySR.Runtime_Type_AttributeNotFound, heapType.Name, name);
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, type.TpName, name);
        }

        // deleting a dunder re-resolves the slot from the MRO — an ancestor's
        // __hash__ = None keeps the type unhashable, an ancestor's method comes
        // back, nothing anywhere clears the slot — and propagates to subclasses
        // (CPython update_slot through type_update_dict's delete path)
        if (self is PyTypeObject typeObj)
            UpdateSlot(typeObj, name);

        return PyNoneObject.None;
    }


    internal static PyResult DefaultTypeGetAttribute(PyCallContext context, PyTypeObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.TpName);

        var metaType = self.PyType;
        var name = str.Value;

        if (name is PySpecialNames.Class)
            return metaType;

        if (name is PySpecialNames.Dict)
            return self.PyAttributes.Self;

        if (TryLookupAttrInMro(metaType, name, out var metaAttr))
        {
            if (PyUtils.IsDataDescriptor(metaAttr))
            {
                var getFunc = metaAttr.PyType.Slots.Get;
                if (getFunc is not null)
                    return getFunc(context, metaAttr, self, metaType);
            }
        }

        if (TryLookupAttrInMro(self, name, out var attr))
        {
            var getFunc = attr.PyType.Slots.Get;
            if (getFunc is not null)
                return getFunc(context, attr, PyNoneObject.None, self);

            return attr;
        }

        if (metaAttr is not null)
        {
            var getFunc = metaAttr.PyType.Slots.Get;
            if (getFunc is not null)
                return getFunc(context, metaAttr, self, metaType);

            // CPython _Py_type_getattro_impl: a non-descriptor metatype entry
            // answers once the type's own MRO has missed
            return metaAttr;
        }

        return PyResult.AttributeError(PySR.Runtime_Type_AttributeNotFound, self.TpName, name);
    }

    internal static PyResult DefaultFormat(PyCallContext context, PyObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.TpName);

        if (str.Value.Length is 0)
            return PySpecialMethods.Str(context, self);

        // CPython object.__format__ rejects a non-empty spec with TypeError
        return PyResult.TypeError(PySR.Runtime_Object_FormatUnsupported, self.PyType.TpName);

    }

    internal static PyResult DefaultInit(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython object_init: mirror of object_new — excess arguments are
        // an error unless a custom __new__ (which defines the signature)
        // consumed them
        if (args.Count is not 0 || kwargs.Count is not 0)
        {
            var type = self.PyType;
            if (!ReferenceEquals(type.Slots.Init, PyObjectType.Shared.Slots.Init))
                return PyResult.TypeError(PySR.Runtime_Object_InitTakesExactlyOneArg);
            if (ReferenceEquals(type.Slots.New, PyObjectType.Shared.Slots.New))
                return PyResult.TypeError(PySR.Runtime_Object_TypeInitTakesExactlyOneArg, type.Name);
        }

        return PyNoneObject.None;
    }
    internal static PyResult DefaultBinaryOperator(PyCallContext context, PyObject self, PyObject other)
    {
        return PyNotImplementedObject.NotImplemented;
    }
    internal static PyResult DefaultEq(PyCallContext context, PyObject self, PyObject other)
    {
        if (ReferenceEquals(self, other))
            return PyBoolObject.True;

        return PyNotImplementedObject.NotImplemented;
    }
    internal static PyResult DefaultNe(PyCallContext context, PyObject self, PyObject other)
    {
        var eq = PyOperators.Eq(context, self, other);
        if (eq.IsError || eq.IsNotImplemented)
            return eq;

        return PyOperators.Not(context, eq.Value);
    }
}
