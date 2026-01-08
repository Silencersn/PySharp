using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Xml.Linq;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject
{
    internal PyResult DefaultRepr(PyCallContext context, PyObject self)
    {
        return PyStrObject.FromString($"<{FullName} object at 0x{self.PyId:X16}>");
    }
    internal static PyResult DefaultStr(PyCallContext context, PyObject self)
    {
        return PySpecialMethods.Repr(context, self);
    }
    internal static PyResult DefaultBool(PyCallContext context, PyObject self)
    {
        return PyBoolObject.True;
    }
    internal static PyResult DefaultHash(PyCallContext context, PyObject self)
    {
        return PyIntObject.FromInteger(self.GetHashCode());
    }
    internal static PyResult DefaultGetAttribute(PyCallContext context, PyObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.RaiseTypeError($"attribute name must be string, not '{item.PyType.FullName}'");

        var type = self.PyType;
        var name = str.Value;

        if (name is PySpecialNames.Class)
            return type;

        // TODO: __dict__ and others

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            if (Utils.IsDataDescriptor(attr))
            {
                var getFunc = attr.PyType.Slots.Get;
                if (getFunc is not null)
                    return getFunc(context, attr, self, type);
            }
        }

        if (self._pyAttributes?.TryGetValue(name, out var value) is true)
            return value;

        if (attr is not null)
        {
            var getFunc = attr.PyType.Slots.Get;
            if (getFunc is not null)
                return getFunc(context, attr, self, type);

            return attr;
        }

        return PyResult.RaiseAttributeError($"'{self.PyType.Name}' object has no attribute '{name}'");
    }

    internal static PyResult DefaultSetAttr(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        if (key is not PyStrObject str)
            return PyResult.RaiseTypeError($"attribute name must be string, not '{key.PyType.FullName}'");

        var type = self.PyType;
        var name = str.Value;

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            var func = attr.PyType.Slots.Set;
            if (func is not null)
                return func(context, attr, self, value);
        }

        self.PyAttributes[name] = value;
        return PyNoneObject.None;
    }

    internal static PyResult DefaultDelAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.RaiseTypeError($"attribute name must be string, not '{item.PyType.FullName}'");

        var type = self.PyType;
        var name = str.Value;

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            var func = attr.PyType.Slots.Delete;
            if (func is not null)
                return func(context, attr, self);
        }

        var removed = self.PyAttributes.Remove(name);
        if (!removed)
            return PyResult.RaiseAttributeError($"'{type.Name}' object has no attribute '{name}'");

        return PyNoneObject.None;
    }


    internal static PyResult DefaultTypeGetAttribute(PyCallContext context, PyTypeObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.RaiseTypeError($"attribute name must be string, not '{item.PyType.FullName}'");

        var type = self.PyType;
        var name = str.Value;

        if (name is PySpecialNames.Class)
            return type;

        // TODO: __dict__ and others

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            if (Utils.IsDataDescriptor(attr))
            {
                var getFunc = attr.PyType.Slots.Get;
                if (getFunc is not null)
                    return getFunc(context, attr, PyNoneObject.None, self);
            }
        }

        if (self._pyAttributes?.TryGetValue(name, out var value) is true)
            return value;

        if (attr is not null)
            return attr;

        if (type is not PyTypeObjectType && type.Slots.GetAttribute is not null)
            return type.Slots.GetAttribute(context, self, item);

        return PyResult.RaiseAttributeError($"'{self.PyType.Name}' object has no attribute '{name}'");
    }

    internal static PyResult DefaultFormat(PyCallContext context, PyObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject str)
            return PyResult.RaiseTypeError($"format() argument 2 must be str, not {formatSpec.PyType.FullName}");

        if (str.Value.Length is 0)
            return self.PyType.Str(context, self);

        return PyResult.RaiseValueError($"unsupported format string passed to {self.PyType.FullName}.__format__");

    }
}
