using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;

namespace PySharp.PyModules.Builtins;

public class PySuperObject : PyObject
{
    private readonly PyTypeObject? _type;
    private readonly PyObject? _object;
    private readonly IReadOnlyList<PyTypeObject> _searchList;

    public PySuperObject(PyTypeObject? type, PyObject? obj, IReadOnlyList<PyTypeObject> searchList)
    {
        _type = type;
        _object = obj;
        _searchList = searchList;
    }

    public static PySuperObject? CreateSuper(PyTypeObject type, PyObject objectOrType)
    {
        if (objectOrType is PyTypeObject pyTypeObject)
        {
            var mro = pyTypeObject.MRO;
            for (int i = 0; i < mro.Count; i++)
            {
                if (mro[i] == type)
                    return new PySuperObject(pyTypeObject, null, [.. mro.Skip(i + 1)]);
            }
        }
        else
        {
            var mro = objectOrType.PyType.MRO;
            for (int i = 0; i < mro.Count; i++)
            {
                if (mro[i] == type)
                    return new PySuperObject(null, objectOrType, [.. mro.Skip(i + 1)]);
            }
        }

        PyVirtualMachine.RaiseTypeError("super(type, obj): obj must be an instance or subtype of type");
        return null;
    }

    private PyObject? GetProxyAttr(string name, PyObject instance, PyObject owner)
    {
        PyObject? attrFromType = null;

        foreach (var pyType in _searchList)
        {
            if (pyType.PyAttributes.TryGetValue(name, out attrFromType))
                break;
        }

        PyObject? nonDataDescriptor = null;
        if (attrFromType is not null && Utils.IsDescriptor(attrFromType, out var hasGet, out var hasSet, out var hasDelete))
        {
            if (hasGet)
            {
                if (hasSet || hasDelete)
                    return attrFromType.Get(instance, owner);

                nonDataDescriptor = attrFromType;
            }
        }

        if (nonDataDescriptor is not null)
            return nonDataDescriptor.Get(instance, owner);

        if (attrFromType is not null)
            return attrFromType;

        return base.GetAttribute(name);
    }

    public override PyObject? GetAttribute(string item)
    {
        if (_object is not null)
        {
            return GetProxyAttr(item, _object, _object.PyType);
        }
        else
        {
            Debug.Assert(_type is not null);
            return GetProxyAttr(item, PyNoneObject.None, _type);
        }
    }
}


public sealed class PySuperObjectType : PyTypeObject
{
    public override string Name => "super";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("type", "object_or_type", "/")]
    private static PyObject? NewImpl(PyArguments arguments)
    {
        if (arguments[0] is not PyTypeObject type)
            return PyVirtualMachine.RaiseTypeError($"super() argument 1 must be a type, not {arguments[0].PyType.Name}");

        return PySuperObject.CreateSuper(type, arguments[1]);
    }

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}
