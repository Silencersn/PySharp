using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Utility;

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
        return PyBoolObject.True;
    }
    internal static PyResult DefaultHash(PyCallContext context, PyObject self)
    {
        return PyIntObject.FromInteger(self.GetHashCode());
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
            if (self is PyObjectManagedDict managed)
                return PyDictObject.CreateProxy(new DictAdapter(managed.PyAttributes!));
            return PyDictObject.CreateProxy(new DictAdapter(self.PyAttributes!));
        }

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            if (Utils.IsDataDescriptor(attr))
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

        if (TryLookupAttrInMro(type, name, out var attr))
        {
            var func = attr.PyType.Slots.Set;
            if (func is not null)
                return func(context, attr, self, value);
        }

        self.PyAttributes[name] = value;

        // When setting an attribute on a type object (e.g. cls.__init__ = func),
        // also update the corresponding slot so that Call/New etc. pick it up.
        if (self is PyTypeObject typeObj)
            typeObj.Slots.TrySetSlot(name, value);

        return PyNoneObject.None;
    }

    internal static PyResult DefaultDelAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.FullName);

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
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, type.FullName, name);

        return PyNoneObject.None;
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
        {
            if (self is PyObjectManagedDict managed)
                return PyDictObject.CreateProxy(new DictAdapter(managed.PyAttributes!));
            return PyDictObject.CreateProxy(new DictAdapter(self.PyAttributes!));
        }

        if (TryLookupAttrInMro(metaType, name, out var metaAttr))
        {
            if (Utils.IsDataDescriptor(metaAttr))
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
        }

        return PyResult.AttributeError(PySR.Runtime_Type_AttributeNotFound, self.FullName, name);
    }

    internal static PyResult DefaultFormat(PyCallContext context, PyObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.FullName);

        if (str.Value.Length is 0)
            return PySpecialMethods.Str(context, self);

        return PyResult.ValueError(PySR.Runtime_Object_FormatUnsupported, self.PyType.FullName);

    }

    internal static PyResult DefaultInit(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
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
