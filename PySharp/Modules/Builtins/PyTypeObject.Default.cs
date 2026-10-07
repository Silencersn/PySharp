using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics;

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

        // __class__ reaches the getset on object's dict through the MRO, so a
        // class body, base or property shadowing the name wins first — the
        // same lookup path a plain attribute takes

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            if (PyUtils.IsDataDescriptor(attr))
            {
                var getFunc = attr.PyType.Slots.Get;
                if (getFunc is not null)
                    return getFunc(context, attr, self, type);
            }
        }

        if (self.PyAttributes.TryGetValue(name, out var value))
            return value;

        if (attr is not null)
        {
            var getFunc = attr.PyType.Slots.Get;
            if (getFunc is not null)
                return getFunc(context, attr, self, type);

            return attr;
        }

        // CPython has no __dict__ entry on object: heap classes answer through
        // their own subtype_dict getset installed in their dict, everything
        // else — built-in values, builtin functions, methods, code, ... —
        // falls through to here. type.__dict__ resolves on the metatype before
        // this path ever runs
        if (name is PySpecialNames.Dict)
        {
            // Objects without a genuine instance dict (built-in values, builtin
            // functions, methods, code, ...) have no __dict__ (CPython).
            if (self.IsImmutable)
                return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.TpName, name);

            // CPython type.__dict__ is a getset wrapping the namespace in a
            // fresh mappingproxy on every read (typeobject.c type_dict); the
            // mutable face stays on setattr/delattr
            if (self is PyTypeObject)
                return new PyMappingProxyObject(self.PyAttributes.Self);

            return self.PyAttributes.Self;
        }

        // the sentence stays clean: CPython appends the hint at display
        // time (_Py_Offer_Suggestions), so str(exc) matches too
        return AttributeNotFound(self, name);
    }

    // calculate_attribute_suggestions: the candidates are the names on
    // the type's MRO dicts, and a module suggests nothing (math.sqtr
    // stays bare)
    internal static PyResult AttributeNotFound(PyObject self, string name)
    {
        var result = PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.TpName, name);
        if (self is not PyModuleObject && result.Exception is { } error)
            error.DisplaySuggestion = PyNameSuggestions.Calculate(AttributeCandidatesOf(self.PyType), name);
        return result;
    }

    internal static string[] AttributeCandidatesOf(PyTypeObject type)
    {
        List<string> candidates = [];
        foreach (var baseType in type.MRO)
        {
            foreach (var member in baseType.PyAttributes)
                candidates.Add(member.Key);
        }
        return [.. candidates];
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

        // __class__ writes reach the object getset through the MRO lookup
        // above; a class body shadowing the name installs its own descriptor
        // and answers before object's ever does

        // CPython _PyObject_GenericSetAttrWithDict: dict-less instances
        // (IsImmutable) reject the write before any instance-dict handling
        if (self.IsImmutable)
            return FrozenAttrWriteError(type, name, attr);

        // CPython subtype_setdict -> _PyObject_SetDict: the value replaces the
        // whole instance dict (PyDict_Check accepts a subclass), and a type
        // object reads __dict__ through a getset with no setter. The whole
        // replacement belongs to the getset on the MRO; a class body entry
        // shadowing __dict__ has no setter and falls through to the plain
        // instance-dict write, so only an unshadowed name lands here
        if (name is PySpecialNames.Dict && attr is null)
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

    // CPython object_set_class (Objects/typeobject.c), the setter of the
    // __class__ getset on object's dict: the value must be a class, both
    // sides must be mutable (ModuleType subclasses qualify) and share a
    // layout, and only the type pointer moves — __init__ never runs and the
    // instance dict stays
    internal static PyResult SetClassAttribute(PyObject self, PyObject value)
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

    // subtype_getsets_dict_only (Objects/typeobject.c): the __dict__ getset
    // every heap type installs in its own dict — get hands out the live
    // instance dict, set replaces it whole, delete empties it. Because the
    // descriptor sits on the MRO, a class body or property shadowing
    // __dict__ wins first, exactly like any other name
    internal static PyMemberDescriptorObject CreateInstanceDictDescriptor(PyTypeObject declaringType)
    {
        return new PyMemberDescriptorObject(
            PyGetSetDescriptorObjectType.Shared,
            declaringType,
            PySpecialNames.Dict,
            Get_InstanceDict,
            Set_InstanceDict,
            Delete_InstanceDict);
    }

    private static PyResult Get_InstanceDict(PyCallContext context, PyObject self)
        => self.PyAttributes.Self;

    // CPython subtype_setdict -> _PyObject_SetDict: the value replaces the
    // whole instance dict (PyDict_Check accepts a subclass)
    private static PyResult Set_InstanceDict(PyCallContext context, PyObject self, PyObject value)
    {
        if (value is not PyDictObject assigned)
            return PyResult.TypeError(PySR.Runtime_Object_DictMustBeDictionary, value.PyType.Name);

        self.PyAttributes = assigned;
        return PyNoneObject.None;
    }

    // CPython subtype_setdict with a NULL value drops the dict: the next
    // read materializes an empty one, the dropped dict keeps its items
    private static PyResult Delete_InstanceDict(PyCallContext context, PyObject self)
    {
        self.PyAttributes = new PyDictObject();
        return PyNoneObject.None;
    }

    // PyMemberDef slots from __slots__ (type_add_members): storage rides the
    // same attached-properties channel that backs every instance dict — the
    // dict-less IsImmutable face only hides it from __dict__/setattr, and
    // these member descriptors are the sole access path
    internal static PyMemberDescriptorObject CreateSlotDescriptor(PyTypeObject declaringType, string name)
    {
        return new PyMemberDescriptorObject(
            PyMemberDescriptorObjectType.Shared,
            declaringType,
            name,
            (context, instance) => Get_Slot(context, instance, name),
            (context, instance, value) => Set_Slot(instance, name, value),
            (context, instance) => Delete_Slot(instance, name));
    }

    private static PyResult Get_Slot(PyCallContext context, PyObject instance, string name)
    {
        var dict = PyAttachedPropertiesManager.Shared.GetDict(instance);
        if (dict.TryGetValue(name, out var value))
            return value;

        // PyMember_GetOne (Py_T_OBJECT_EX): an unset slot reads as a plain
        // missing attribute
        return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, instance.PyType.TpName, name);
    }

    private static PyResult Set_Slot(PyObject instance, string name, PyObject value)
    {
        PyAttachedPropertiesManager.Shared.GetDict(instance)[name] = value;
        return PyNoneObject.None;
    }

    // deleting an unset slot reports the bare name (3.14 member face)
    private static PyResult Delete_Slot(PyObject instance, string name)
    {
        if (!PyAttachedPropertiesManager.Shared.GetDict(instance).Remove(name))
            return PyResult.AttributeError(name);

        return PyNoneObject.None;
    }

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

        // __class__ deletes reach the object getset through the MRO lookup
        // above, whose deleter refuses before any instance-dict handling

        // same dict-less gate as the set path; CPython reports the same two
        // AttributeError shapes for deletion too
        if (self.IsImmutable)
            return FrozenAttrWriteError(type, name, attr);

        // CPython subtype_setdict with a NULL value drops the dict: the next
        // read materializes an empty one, the dropped dict keeps its items.
        // A class body entry shadowing __dict__ falls through to the plain
        // instance-dict delete instead
        if (name is PySpecialNames.Dict && attr is null)
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

        // CPython _Py_type_getattro_impl: a data descriptor on the metatype
        // MRO answers first, ahead of the class's own entries — object's
        // __class__ getset and type's __dict__ getset both resolve here, so
        // a class body cannot shadow the class object's own faces
        if (name is PySpecialNames.Class)
            return metaType;

        if (name is PySpecialNames.Dict)
            return new PyMappingProxyObject(self.PyAttributes.Self);

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

        // the class-level miss suggests from the class's own MRO dicts
        // (A.methd still points at 'method')
        var typeResult = PyResult.AttributeError(PySR.Runtime_Type_AttributeNotFound, self.TpName, name);
        if (typeResult.Exception is { } typeError)
            typeError.DisplaySuggestion = PyNameSuggestions.Calculate(AttributeCandidatesOf(self), name);
        return typeResult;
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
    // slot_tp_richcompare: the dict-driven comparison entry installed for
    // runtime classes resolving a comparison dunder. The shared delegate
    // looks the op's own dunder up on the receiver's MRO at call time, so
    // later dict mutations stay visible without re-resolution — the
    // converted value is deliberately not captured (the TrySetSlot switch
    // routes every one of the six names here).
    internal static readonly PyRichCompareFunction LookupRichCompare = LookupRichCompareCore;

    private static PyResult LookupRichCompareCore(PyCallContext context, PyObject self, PyObject other, PyOperatorTypes op)
    {
        var name = op switch
        {
            PyOperatorTypes.Lt => PySpecialNames.Lt,
            PyOperatorTypes.LtE => PySpecialNames.Le,
            PyOperatorTypes.Eq => PySpecialNames.Eq,
            PyOperatorTypes.NotEq => PySpecialNames.Ne,
            PyOperatorTypes.Gt => PySpecialNames.Gt,
            PyOperatorTypes.GtE => PySpecialNames.Ge,
            _ => throw new UnreachableException(),
        };

        foreach (var type in self.PyType.InternalMRO)
        {
            if (type.PyAttributes.TryGetValue(name, out var value))
                return value.Call(context, [self, other]);
        }

        return PyNotImplementedObject.NotImplemented;
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
