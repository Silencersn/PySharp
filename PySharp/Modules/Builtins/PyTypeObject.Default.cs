using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject
{
    internal static PyResult DefaultRepr(PyCallContext context, PyObject self)
    {
        return PyStrObject.FromString($"<{self.PyType.FullName} object at 0x{self.PyId:X16}>");
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
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.FullName);

        var type = self.PyType;
        var name = str.Value;

        if (name is PySpecialNames.Class)
            return type;

        if (name is PySpecialNames.Dict)
        {
            // Objects without a genuine instance dict (built-in values, builtin
            // functions, methods, code, ...) have no __dict__ (CPython).
            if (self.IsImmutable)
                return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.FullName, name);
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

        return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.FullName, name);
    }

    internal static PyResult DefaultSetAttr(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        if (key is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, key.PyType.FullName);

        var type = self.PyType;
        var name = str.Value;

        // CPython type_setattro: the immutable-type check fires before any
        // descriptor or instance-dict handling
        if (self is PyTypeObject ownerType && !ownerType.IsRuntimeCreated)
            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, name, ownerType.FullName);

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

        // When setting an attribute on a type object (e.g. cls.__init__ = func),
        // also update the corresponding slot so that Call/New etc. pick it up.
        if (self is PyTypeObject typeObj && !IsObjectDefaultSlotValue(name, value))
        {
            if (name is PySpecialNames.Hash)
                SetHashSlot(typeObj.Slots, value);
            else
                typeObj.Slots.TrySetSlot(name, value);
        }

        return PyNoneObject.None;
    }

    // CPython _PyObject_GenericSetAttrWithDict: dict-less instances reject
    // attribute writes with two AttributeError shapes — a name found in the
    // MRO without a setter is "read-only", anything else is the shared
    // "no attribute and no __dict__" error (assignment and deletion alike)
    private static PyResult FrozenAttrWriteError(PyTypeObject type, string name, PyObject? attr)
    {
        if (attr is not null)
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeReadOnly, type.FullName, name);

        return PyResult.AttributeError(PySR.Runtime_Object_AttributeNoDict, type.FullName, name);
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

    // CPython fixup_slot_dispatchers: assigning the inherited object default
    // (__new__ / __init__) is a no-op for slot wiring — the slot keeps
    // inheriting object's delegate, so the excess-argument checks in
    // object.__new__/__init__ still see the defaults
    internal static bool IsObjectDefaultSlotValue(string name, PyObject value)
    {
        if (name is not (PySpecialNames.New or PySpecialNames.Init))
            return false;

        return TryLookupAttrInMro(PyObjectType.Shared, name, out var defaultValue)
            && ReferenceEquals(value, defaultValue);
    }

    // CPython resolves __new__/__init__ by MRO lookup at call time, while the
    // eager slot fill bakes inherited delegates at class-creation order: a
    // base defining __init__ could lose to a base created earlier whose slot
    // was already filled with the built-in. Re-resolve from the first MRO
    // entry defining the method in its own dict.
    internal static void RecomputeConstructionSlots(PyTypeObject type)
    {
        RecomputeConstructionSlot(PySpecialNames.Init,
            static t => t.Slots.Init, static (t, f) => t.Slots.Init = f, type);
        RecomputeConstructionSlot(PySpecialNames.New,
            static t => t.Slots.New, static (t, f) => t.Slots.New = f, type);
    }

    private static void RecomputeConstructionSlot<T>(string name, Func<PyTypeObject, T?> getSlot, Action<PyTypeObject, T> setSlot, PyTypeObject type) where T : Delegate
    {
        foreach (var entry in type.InternalMRO)
        {
            if (!entry.PyAttributes.TryGetValue(name, out var value))
                continue;

            // assigning object's defaults is a no-op for slot wiring
            if (IsObjectDefaultSlotValue(name, value))
                return;

            var slot = getSlot(entry);
            if (slot is not null)
                setSlot(type, slot);
            return;
        }
    }

    // CPython fixup_slot_dispatchers: each special-method slot resolves
    // through the first MRO entry defining the dunder in its own dict.
    // The eager FillNullWith pass bakes inherited copies of object's
    // default implementations into base slots, and such a copy then masks
    // a later base's real method (a non-first parent's __ne__/__gt__/
    // __ge__/__hash__ was unreachable), so re-resolve the defaultable
    // family from the MRO dicts. A non-runtime-created base ends the walk:
    // its slots are authoritative for everything behind it, and its dict
    // misses (dunders it models purely through C# slots) must not let a
    // later base override it.
    private static readonly string[] DefaultableSlotNames =
    [
        PySpecialNames.Repr,
        PySpecialNames.Str,
        PySpecialNames.Hash,
        PySpecialNames.Eq,
        PySpecialNames.Ne,
        PySpecialNames.Lt,
        PySpecialNames.Le,
        PySpecialNames.Gt,
        PySpecialNames.Ge,
    ];

    internal static void FixupSlotDispatchers(PyTypeObject type)
    {
        foreach (var name in DefaultableSlotNames)
        {
            for (int i = 0; i < type.InternalMRO.Length; i++)
            {
                var entry = type.InternalMRO[i];
                if (!entry.IsRuntimeCreated)
                    break;

                if (!entry.PyAttributes.TryGetValue(name, out var value))
                    continue;

                // index 0 is the type itself: its own dict entry is already
                // wired by the namespace scan, so an own-dict hit only ends
                // the walk — rewiring it here would lose the main loop's
                // exact delegate
                if (i > 0)
                {
                    if (name is PySpecialNames.Hash)
                        SetHashSlot(type.Slots, value);
                    else
                        type.Slots.TrySetSlot(name, value);
                }
                break;
            }
        }
    }

    internal static PyResult DefaultDelAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.FullName);

        var type = self.PyType;
        var name = str.Value;

        // same immutable-type gate as the set path; the delete path reports
        // "cannot set" in CPython too
        if (self is PyTypeObject ownerType && !ownerType.IsRuntimeCreated)
            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, name, ownerType.FullName);

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
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, type.FullName, name);
        }

        // deleting an explicit __hash__ re-inherits the slot through the MRO
        // (CPython fixup_slot_dispatchers): an ancestor's None keeps the type
        // unhashable, otherwise object's identity hash comes back
        if (self is PyTypeObject hashOwner && name is PySpecialNames.Hash)
            RecomputeHashSlot(hashOwner);

        return PyNoneObject.None;
    }

    private static void RecomputeHashSlot(PyTypeObject type)
    {
        foreach (var entry in type.InternalMRO)
        {
            if (ReferenceEquals(entry, type))
                continue;

            if (entry.Slots.Hash is { } inherited)
            {
                type.Slots.Hash = inherited;
                return;
            }
        }
    }


    internal static PyResult DefaultTypeGetAttribute(PyCallContext context, PyTypeObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.FullName);

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

        return PyResult.AttributeError(PySR.Runtime_Type_AttributeNotFound, self.FullName, name);
    }

    internal static PyResult DefaultFormat(PyCallContext context, PyObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.FullName);

        if (str.Value.Length is 0)
            return PySpecialMethods.Str(context, self);

        // CPython object.__format__ rejects a non-empty spec with TypeError
        return PyResult.TypeError(PySR.Runtime_Object_FormatUnsupported, self.PyType.FullName);

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
