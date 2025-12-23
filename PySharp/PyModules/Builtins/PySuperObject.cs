using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Builtins;

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
public class PySuperObject : PyObject, IPyDescriptor
{
    internal readonly PyTypeObject _type;
    internal readonly PyObject _object;

    internal PySuperObject(PyTypeObject type, PyObject obj)
    {
        _type = type;
        _object = obj;
    }

    bool IPyDescriptor.SupportsGet => true;
    bool IPyDescriptor.SupportsSet => false;
    bool IPyDescriptor.SupportsDelete => false;

    public static PyResult CreateSuper(PyTypeObject type, PyObject objectOrType)
    {
        if (objectOrType is PyTypeObject pyTypeObject && pyTypeObject.IsSubclassOf(type))
            return new PySuperObject(type, objectOrType);

        if (type.IsInstance(objectOrType))
            return new PySuperObject(type, objectOrType);

        return PyResult.RaiseTypeError("super(type, obj): obj must be an instance or subtype of type");
    }
}

public sealed class PySuperObjectType : PyTypeObject<PySuperObjectType, PySuperObject>
{
    public override string Name => "super";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef()]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        var frame = PyVirtualMachine.CurrentFrame;
        if (frame.CallingArguments is null)
            // TODO: in what situations would this happen?
            return PyResult.RaiseRuntimeError("super(): no arguments");

        var (args, _) = frame.CallingArguments.Value;
        if (args.Count is 0)
            return PyResult.RaiseRuntimeError("super(): no arguments");

        if (frame.InternalClosure is null || !frame.InternalClosure.TryGetValue(PySpecialNames.Class, out var cell))
            return PyResult.RaiseRuntimeError("super(): __class__ cell not found");

        if (cell.Value is null)
            return PyResult.RaiseRuntimeError("super(): empty __class__ cell");

        if (cell.Value is not PyTypeObject type)
            return PyResult.RaiseRuntimeError($"super(): __class__ is not a type ({cell.Value.PyType.Name})");

        return PySuperObject.CreateSuper(type, args[0]);
    }

    [PyFunctionArgsDef("type", "object_or_type=None", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyTypeObject type)
            return PyResult.RaiseTypeError($"super() argument 1 must be a type, not {arguments[0].PyType.Name}");

        return PySuperObject.CreateSuper(type, arguments[1]);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected internal override PyResult GetAttribute(PyCallContext context, PySuperObject self, string item)
    {
        PyTypeObject startType = self._type.IsInstance(self._object) ? self._object.PyType : (PyTypeObject)self._object;
        var iter = startType.MRO.GetEnumerator();
        while (iter.MoveNext())
        {
            if (iter.Current == self._type)
                break;
        }
        while (iter.MoveNext())
        {
            var pyType = iter.Current;
            if (pyType.PyAttributes.TryGetValue(item, out var attr))
            {
                if (Utils.IsDescriptor(attr, out var hasGet, out _, out _) && hasGet)
                    return attr.Get(context, self._object, PyNoneObject.None);
                return attr;
            }
        }
        return PyResult.RaiseAttributeError(item);
    }

    protected internal override PyResult Get(PyCallContext context, PySuperObject self, PyObject instance, PyObject owner)
    {
        if (self._object is PyNoneObject && instance is not PyNoneObject)
            return new PySuperObject(self._type, instance);
        return self;
    }

    protected internal override PyResult Repr(PyCallContext context, PySuperObject self)
    {
        return PyStrObject.FromString($"<super: {self._type.Name}, {self._object.PyType.Name}>");
    }

    protected internal override PyResult Bool(PyCallContext context, PySuperObject self)
    {
        return PyBoolObject.True;
    }
}
