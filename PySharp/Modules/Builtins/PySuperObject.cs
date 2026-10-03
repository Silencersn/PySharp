using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

// pure python equivalents (from https://www.python.org/download/releases/2.2.3/descrintro/#cooperation): 
// class Super(object):
//     def __init__(self, type, obj=None):
//         self.__type__ = type
//         self.__obj__ = obj
//     def __get__(self, obj, type=None):
//         if self.__obj__ is None and obj is not None:
//             return Super(self.__type__, obj)
//         else:
//             return self
//     def __getattr__(self, attr):
//         if isinstance(self.__obj__, self.__type__):
//             starttype = self.__obj__.__class__
//         else:
//             starttype = self.__obj__
//         mro = iter(starttype.__mro__)
//         for cls in mro:
//             if cls is self.__type__:
//                 break
//         # Note: mro is an iterator, so the second loop
//         # picks up where the first one left off!
//         for cls in mro:
//             if attr in cls.__dict__:
//                 x = cls.__dict__[attr]
//                 if hasattr(x, "__get__"):
//                     x = x.__get__(self.__obj__)
//                 return x
//         raise AttributeError(attr)
// 
public class PySuperObject : PyObject
{
    internal readonly PyTypeObject _type;
    internal readonly PyObject _object;

    // supercheck's third branch: the __class__ a proxy reports becomes the
    // cached search start (CPython's obj_type)
    private readonly PyTypeObject? _classOverride;

    public override PyTypeObject DefaultPyType => PySuperObjectType.Shared;

    internal PySuperObject(PyTypeObject type, PyObject obj, PyTypeObject? classOverride = null)
    {
        _type = type;
        _object = obj;
        _classOverride = classOverride;
    }

    // supercheck's return value, which CPython caches as obj_type: the obj
    // itself when it is a class, its type otherwise, or the __class__ a
    // proxy reports
    internal static PyTypeObject SelfClassOf(PySuperObject self)
    {
        if (self._classOverride is { } overrideType)
            return overrideType;
        return self._type.IsInstance(self._object) ? self._object.PyType : (PyTypeObject)self._object;
    }

    public static PyResult CreateSuper(PyCallContext context, PyTypeObject type, PyObject objectOrType)
    {
        // super_init_impl folds obj is None into an unbound super before
        // supercheck would reject it (Objects/typeobject.c:12182)
        if (objectOrType is PyNoneObject)
            return new PySuperObject(type, objectOrType);

        if (objectOrType is PyTypeObject pyTypeObject && pyTypeObject.IsSubclassOf(type))
            return new PySuperObject(type, objectOrType);

        if (type.IsInstance(objectOrType))
            return new PySuperObject(type, objectOrType);

        // supercheck's third branch (typeobject.c:11973, "This will allow
        // using super() with a proxy for obj"): the __class__ a proxy reports
        // through the type MRO — a property binds here, a plain class
        // attribute reports itself, the instance dict stays out — becomes the
        // search start when it is a type different from the real one and a
        // subclass of cls
        if (PyObject.TryLookupAttrInMro(objectOrType.PyType, PySpecialNames.Class, out var classEntry))
        {
            PyObject reported;
            var getFunc = classEntry.PyType.Slots.Get;
            if (getFunc is not null)
            {
                var classAttr = getFunc(context, classEntry, objectOrType, objectOrType.PyType);
                reported = classAttr.IsError ? null! : classAttr.Value;
            }
            else
            {
                reported = classEntry;
            }

            if (reported is PyTypeObject overrideType
                && !ReferenceEquals(overrideType, objectOrType.PyType)
                && overrideType.IsSubclassOf(type))
                return new PySuperObject(type, objectOrType, overrideType);
        }

        // CPython supercheck names both sides of the check and picks the
        // wording by whether obj is itself a type (Objects/typeobject.c:11993),
        // both names cut to 200 UTF-8 bytes
        var typeOrInstance = objectOrType is PyTypeObject ? "type" : "instance of";
        var objectName = PyUtils.TruncateUtf8(
            objectOrType is PyTypeObject objType ? objType.TpName : objectOrType.PyType.TpName, 200);
        return PyResult.TypeError(
            PySR.Runtime_Super_ObjNotMatchType, typeOrInstance, objectName, PyUtils.TruncateUtf8(type.TpName, 200));
    }
}

[PyType("super")]
public sealed partial class PySuperObjectType : PyTypeObject<PySuperObject>
{

    [PyExport(PySpecialNames.New, nameof(NewImpl_1), nameof(NewImpl_2))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters()]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        var frame = context.CurrentInternalFrame;
        var variables = frame.Variables;
        var code = frame.CodeObject;

        // CPython super_init_without_args: the zero-argument form is filled
        // from the calling frame. LocalsSpan[0] is the first locals-plus
        // entry, not the first parameter, so the decision must come from
        // the argument count (module/class/eval frames have no code object
        // or no positional parameters).
        if (code is null || !variables.HasLocals || code.ArgCount is 0)
            return PyResult.RuntimeError(PySR.Runtime_Super_NoArgs);

        var objectOrType = variables.LocalsSpan[0];

        // a captured first parameter is MakeCell'd in place (CPython
        // CO_FAST_CELL kind); super() sees through the cell
        if (objectOrType is PyCellObject firstCell && code.CellVars.Contains(code.VarNames[0]))
            objectOrType = firstCell.Value;

        if (objectOrType is null)
            return PyResult.RuntimeError(PySR.Runtime_Super_Arg0Deleted);

        var cellResult = variables.LoadLocal(PySpecialNames.Class);
        if (cellResult.IsError || cellResult.Value is not PyCellObject cell)
            return PyResult.RuntimeError(PySR.Runtime_Super_ClassCellNotFound);

        if (cell.Value is null)
            return PyResult.RuntimeError(PySR.Runtime_Super_ClassCellEmpty);

        if (cell.Value is not PyTypeObject type)
            return PyResult.RuntimeError(PySR.Format(PySR.Runtime_Super_ClassNonType, cell.Value.PyType.TpName));

        return PySuperObject.CreateSuper(context, type, objectOrType);
    }

    [PyFunctionParameters("type", "object_or_type=None", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyTypeObject type)
            return PyResult.TypeError(PySR.Runtime_Super_Arg1MustBeType, arguments[0].PyType.Name);

        return PySuperObject.CreateSuper(context, type, arguments[1]);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult GetAttribute(PyCallContext context, PySuperObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.TpName);

        // super_getattro reads __class__ through GenericGetAttr so the
        // answer is super's own class even when the MRO carries a shadow
        if (str.Value is PySpecialNames.Class)
            return base.GetAttribute(context, self, item);

        // an unbound super has no obj_type (obj is None was folded to NULL
        // at init), so there is no MRO to walk and the read falls through
        // to super's own members (do_super_lookup's skip branch)
        if (self._object is PyNoneObject)
            return base.GetAttribute(context, self, item);

        PyTypeObject startType = PySuperObject.SelfClassOf(self);
        var iter = startType.MRO.GetEnumerator();
        while (iter.MoveNext())
        {
            var eq = PyComparer.Eq(context, iter.Current, self._type);
            if (eq.IsError)
                return eq;

            if (eq.Value.BoolValue)
                break;
        }
        while (iter.MoveNext())
        {
            var pyType = iter.Current;
            if (pyType.PyAttributes.TryGetValue(str.Value, out var attr))
            {
                var getFunc = attr.PyType.Slots.Get;
                if (getFunc is not null)
                {
                    // do_super_lookup binds through the obj's own type, not
                    // super's first argument: instance mode passes the obj,
                    // class mode (obj is itself the type) passes NULL so a
                    // classmethod binds that class instead of the metaclass
                    PyObject instanceArg = ReferenceEquals(self._object, startType) ? PyNoneObject.None : self._object;
                    return getFunc(context, attr, instanceArg, startType);
                }
                return attr;
            }
        }
        // the MRO walk missed, yet do_super_lookup still answers through
        // GenericGetAttr on the super object itself
        return base.GetAttribute(context, self, item);
    }

    protected override PyResult Get(PyCallContext context, PySuperObject self, PyObject instance, PyObject owner)
    {
        if (self._object is PyNoneObject && instance is not PyNoneObject)
            return new PySuperObject(self._type, instance);
        return self;
    }

    protected override PyResult Repr(PyCallContext context, PySuperObject self)
    {
        // super_repr formats the obj_type supercheck produced; the NULL
        // obj_type an unbound super keeps prints verbatim
        if (self._object is PyNoneObject)
            return PyStrObject.FromString($"<super: <class '{self._type.TpName}'>, NULL>");
        return PyStrObject.FromString(
            $"<super: <class '{self._type.TpName}'>, <{PySuperObject.SelfClassOf(self).TpName} object>>");
    }

    // super_members: the three read-only members answer through
    // GenericGetAttr, so they read even while the super is unbound
    [PyProperty(PySpecialNames.ThisClass)]
    private static PyResult Get_ThisClass(PyCallContext context, PySuperObject self)
    {
        return self._type;
    }

    [PyProperty(PySpecialNames.Self)]
    private static PyResult Get_Self(PyCallContext context, PySuperObject self)
    {
        return self._object;
    }

    [PyProperty(PySpecialNames.SelfClass)]
    private static PyResult Get_SelfClass(PyCallContext context, PySuperObject self)
    {
        // the NULL obj_type CPython leaves while unbound reads as None
        if (self._object is PyNoneObject)
            return PyNoneObject.None;
        return PySuperObject.SelfClassOf(self);
    }
}
