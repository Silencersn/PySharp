using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;
using System.Reflection;

namespace PySharp.PyModules.Builtins;

internal enum PySpecialMethodParametersType
{
    Unknown = 0,
    NoArgs,
    Object,
    String,
    ObjectObject,
    StringObject,
    ArgsKwargs,
}

public sealed class PyMethodDescriptorObject : PyObject
{
    internal readonly PyTypeObject _declaringType;
    internal readonly string _name;
    internal readonly MethodInfo? _method;
    internal readonly PySpecialMethodParametersType _paramType;
    internal readonly MethodInfo/*PyMethod<TObject>*/[]? _methods;

    internal PyBuiltinFunctionOrMethodObject UnboundMethod
    {
        get
        {
            if (field is null)
            {
                if (_methods is not null)
                    return field = new PyBuiltinFunctionOrMethodObject(_name, _declaringType, _methods);

                Debug.Assert(_method is not null);
                Debug.Assert(_paramType is not PySpecialMethodParametersType.Unknown);
                return field = new PyBuiltinFunctionOrMethodObject(_name, ToPyDelegate(_declaringType, _method, _paramType));
            }
            return field;
        }
    }

    public override PyTypeObject DefaultPyType => PyMethodDescriptorObjectType.Shared;

    internal PyMethodDescriptorObject(string name, PyTypeObject declaringType, MethodInfo method, PySpecialMethodParametersType paramType)
    {
        _declaringType = declaringType;
        _name = name;
        _method = method;
        _paramType = paramType;
    }
    internal PyMethodDescriptorObject(string name, Delegate typeDelegate, PySpecialMethodParametersType paramType) : this(name, (PyTypeObject)typeDelegate.Target!, typeDelegate.Method, paramType)
    {
    }
    internal PyMethodDescriptorObject(string name, PyTypeObject type, params MethodInfo/*PyMethod<TObject>*/[] methods)
    {
        _declaringType = type;
        _name = name;
        _methods = methods;
    }

    internal static PyFunction ToPyDelegate(PyTypeObject type, MethodInfo method, PySpecialMethodParametersType paramType)
    {
        Debug.Assert(!method.IsStatic);
        return paramType switch
        {
            PySpecialMethodParametersType.NoArgs => [PyFunctionArgsDef("self")] (context, arguments) =>
            {
                return (PyResult)method.Invoke(type, [context, arguments[0]])!;
            }
            ,
            PySpecialMethodParametersType.Object => [PyFunctionArgsDef("self", "obj0", "/")] (context, arguments) =>
            {
                return (PyResult)method.Invoke(type, [context, arguments[0], arguments[1]])!;
            }
            ,
            PySpecialMethodParametersType.String => [PyFunctionArgsDef("self", "str0", "/")] (context, arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[1], out var str0))
                    return PyResult.CaptureExceptionFromPVM();
                return (PyResult)method.Invoke(type, [context, arguments[0], str0])!;
            }
            ,
            PySpecialMethodParametersType.ObjectObject => [PyFunctionArgsDef("self", "obj0", "obj1", "/")] (context, arguments) =>
            {
                return (PyResult)method.Invoke(type, [context, arguments[0], arguments[1], arguments[2]])!;
            }
            ,
            PySpecialMethodParametersType.StringObject => [PyFunctionArgsDef("self", "str0", "obj1", "/")] (context, arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[1], out var str0))
                    return PyResult.CaptureExceptionFromPVM();
                return (PyResult)method.Invoke(type, [context, arguments[0], str0, arguments[2]])!;
            }
            ,
            PySpecialMethodParametersType.ArgsKwargs => [PyFunctionArgsDef("self", "*args", "**kwargs")] (context, arguments) =>
            {
                return (PyResult)method.Invoke(type, [context, arguments[0], arguments.ExtraArgs, arguments.ExtraKwargs])!;
            }
            ,

            _ => throw new NotSupportedException(),
        };
    }

}

public sealed class PyMethodDescriptorObjectType : PyTypeObject<PyMethodDescriptorObjectType, PyMethodDescriptorObject>
{
    public override string Name => "method_descriptor";

    protected internal override PyResult Get(PyCallContext context, PyMethodDescriptorObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;

        if (!self._declaringType.IsInstance(instance))
            return PyResult.RaiseTypeError($"descriptor '{self._name}' requires a '{self._declaringType.Name}' object but received a '{instance.PyType.Name}'");

        if (self._methods is not null)
            return new PyBuiltinFunctionOrMethodObject(self._name, instance, instance.PyType, self._methods);

        Debug.Assert(self._method is not null);
        Debug.Assert(self._paramType is not PySpecialMethodParametersType.Unknown);
        return new PyBuiltinFunctionOrMethodObject(self._name, instance, instance.PyType, ToPyDelegate(self._declaringType, self._method, instance, self._paramType));
    }

    protected internal override PyResult Call(PyCallContext context, PyMethodDescriptorObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is 0)
            return PyResult.RaiseTypeError($"descriptor '{self._name}' of '{self._declaringType.Name}' object needs an argument");

        if (!self._declaringType.IsInstance(args[0]))
            return PyResult.RaiseTypeError($"descriptor '{self._name}' requires a '{self._declaringType.Name}' object but received a '{args[0].PyType.Name}'");

        return self.UnboundMethod.Call(context, args, kwargs);
    }

    internal static PyFunction ToPyDelegate(PyTypeObject type, MethodInfo method, PyObject target, PySpecialMethodParametersType paramType)
    {
        Debug.Assert(!method.IsStatic);
        return paramType switch
        {
            PySpecialMethodParametersType.NoArgs => [PyFunctionArgsDef()] (context, arguments) =>
            {
                return (PyResult)method.Invoke(type, [context, target])!;
            }
            ,
            PySpecialMethodParametersType.Object => [PyFunctionArgsDef("obj0", "/")] (context, arguments) =>
            {
                return (PyResult)method.Invoke(type, [context, target, arguments[0]])!;
            }
            ,
            PySpecialMethodParametersType.String => [PyFunctionArgsDef("str0", "/")] (context, arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[0], out var str0))
                    return PyResult.CaptureExceptionFromPVM();
                return (PyResult)method.Invoke(type, [context, target, str0])!;
            }
            ,
            PySpecialMethodParametersType.ObjectObject => [PyFunctionArgsDef("obj0", "obj1", "/")] (context, arguments) =>
            {
                return (PyResult)method.Invoke(type, [context, target, arguments[0], arguments[1]])!;
            }
            ,
            PySpecialMethodParametersType.StringObject => [PyFunctionArgsDef("str0", "obj1", "/")] (context, arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[0], out var str0))
                    return PyResult.CaptureExceptionFromPVM();
                return (PyResult)method.Invoke(type, [context, target, str0, arguments[1]])!;
            }
            ,
            PySpecialMethodParametersType.ArgsKwargs => [PyFunctionArgsDef("*args", "**kwargs")] (context, arguments) =>
            {
                return (PyResult)method.Invoke(type, [context, target, arguments.ExtraArgs, arguments.ExtraKwargs])!;
            }
            ,

            _ => throw new NotSupportedException(),
        };
    }
}