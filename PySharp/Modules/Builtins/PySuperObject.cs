using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

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

    internal PySuperObject(PyTypeObject type, PyObject obj)
    {
        _type = type;
        _object = obj;
    }

    public static PyResult CreateSuper(PyTypeObject type, PyObject objectOrType)
    {
        if (objectOrType is PyTypeObject pyTypeObject && pyTypeObject.IsSubclassOf(type))
            return new PySuperObject(type, objectOrType);

        if (type.IsInstance(objectOrType))
            return new PySuperObject(type, objectOrType);

        return PyResult.TypeError(PySR.Runtime_Super_ObjNotMatchType);
    }
}

[PyType("super")]
public sealed partial class PySuperObjectType : PyTypeObject<PySuperObject>
{

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionParameters()]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        var variables = context.CurrentInternalFrame.Variables;

        if (variables.LocalsSpan.Length is 0)
            return PyResult.RuntimeError(PySR.Runtime_Super_NoArgs);

        var objectOrType = variables.LocalsSpan[0];
        Debug.Assert(objectOrType is not null);

        var cellResult = variables.LoadLocal(PySpecialNames.Class);
        if (cellResult.IsError || cellResult.Value is not PyCellObject cell)
            return PyResult.RuntimeError(PySR.Runtime_Super_ClassCellNotFound);

        if (cell.Value is null)
            return PyResult.RuntimeError(PySR.Runtime_Super_ClassCellEmpty);

        if (cell.Value is not PyTypeObject type)
            return PyResult.RuntimeError(PySR.Format(PySR.Runtime_Super_ClassNonType, cell.Value.PyType.FullName));

        return PySuperObject.CreateSuper(type, objectOrType);
    }

    [PyFunctionParameters("type", "object_or_type=None", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyTypeObject type)
            return PyResult.TypeError(PySR.Runtime_Super_Arg1MustBeType, arguments[0].PyType.Name);

        return PySuperObject.CreateSuper(type, arguments[1]);
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
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.FullName);

        PyTypeObject startType = self._type.IsInstance(self._object) ? self._object.PyType : (PyTypeObject)self._object;
        var iter = startType.MRO.GetEnumerator();
        while (iter.MoveNext())
        {
            if (PyObjectComparer.Default.Equals(iter.Current, self._type))
                break;
        }
        while (iter.MoveNext())
        {
            var pyType = iter.Current;
            if (pyType.PyAttributes.TryGetValue(str.Value, out var attr))
            {
                var getFunc = attr.PyType.Slots.Get;
                if (getFunc is not null)
                    return getFunc(context, attr, self._object, PyNoneObject.None);
                return attr;
            }
        }
        return PyResult.AttributeError(str.Value);
    }

    protected override PyResult Get(PyCallContext context, PySuperObject self, PyObject instance, PyObject owner)
    {
        if (self._object is PyNoneObject && instance is not PyNoneObject)
            return new PySuperObject(self._type, instance);
        return self;
    }

    protected override PyResult Repr(PyCallContext context, PySuperObject self)
    {
        return PyStrObject.FromString($"<super: {self._type.Name}, {self._object.PyType.Name}>");
    }

    protected override PyResult Bool(PyCallContext context, PySuperObject self)
    {
        return PyBoolObject.True;
    }
}
