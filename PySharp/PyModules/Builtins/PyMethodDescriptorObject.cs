using PySharp.PyRuntime;
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

public sealed class PyMethodDescriptorObject : PyObject, IPyDescriptor
{
    private readonly string _name;
    private readonly MethodInfo? _method;
    private readonly PySpecialMethodParametersType _paramType;
    private readonly MethodInfo[]? _methods;
    private PyBuiltinFunctionOrMethodObject UnboundMethod
    {
        get
        {
            if (field is null)
            {
                if (_methods is not null)
                    return field = new PyBuiltinFunctionOrMethodObject(_name, [.. _methods.Select(ToPyFunction)]);

                Debug.Assert(_method is not null);
                Debug.Assert(_paramType is not PySpecialMethodParametersType.Unknown);
                return field = new PyBuiltinFunctionOrMethodObject(_name, ToPyFunction(_method, _paramType));
            }
            return field;
        }
    }

    public override PyTypeObject PyType => PyBuiltinTypes.MethodDescriptor;

    bool IPyDescriptor.SupportsGet => true;

    bool IPyDescriptor.SupportsSet => false;

    bool IPyDescriptor.SupportsDelete => false;

    internal PyMethodDescriptorObject(string name, MethodInfo method, PySpecialMethodParametersType paramType)
    {
        _name = name;
        _method = method;
        _paramType = paramType;
    }
    internal PyMethodDescriptorObject(string name, IEnumerable<MethodInfo> methods)
    {
        _name = name;
        _methods = [.. methods];
    }

    public override PyObject? Get(PyObject instance, PyObject owner)
    {
        if (owner is not PyTypeObject pyType)
            return PyVirtualMachine.RaiseTypeError(null);

        if (instance is PyNoneObject)
            return this;

        if (_methods is not null)
            return new PyBuiltinFunctionOrMethodObject(_name, instance, pyType, [.. _methods.Select(method => ToPyFunctionDirectly(method, instance))]);

        Debug.Assert(_method is not null);
        Debug.Assert(_paramType is not PySpecialMethodParametersType.Unknown);
        return new PyBuiltinFunctionOrMethodObject(_name, instance, pyType, ToPyFunction(_method, instance, _paramType));
    }

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return UnboundMethod.Call(args, kwargs);
    }

    private static PyFunction ToPyFunctionDirectly(MethodInfo method, PyObject target)
    {
        return method.CreateDelegate<PyFunction>(target);
    }

    private static PyFunction ToPyFunction(MethodInfo method, PyObject target, PySpecialMethodParametersType paramType)
    {
        return paramType switch
        {
            PySpecialMethodParametersType.NoArgs => [PyFunctionArgsDef()] (arguments) =>
            {
                return (PyObject?)method.Invoke(target, null);
            }
            ,
            PySpecialMethodParametersType.Object => [PyFunctionArgsDef("obj0", "/")] (arguments) =>
            {
                return (PyObject?)method.Invoke(target, [arguments[0]]);
            }
            ,
            PySpecialMethodParametersType.String => [PyFunctionArgsDef("str0", "/")] (arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[0], out var str0))
                    return null;
                return (PyObject?)method.Invoke(target, [str0]);
            }
            ,
            PySpecialMethodParametersType.ObjectObject => [PyFunctionArgsDef("obj0", "obj1", "/")] (arguments) =>
            {
                return (PyObject?)method.Invoke(target, [arguments[0], arguments[1]]);
            }
            ,
            PySpecialMethodParametersType.StringObject => [PyFunctionArgsDef("str0", "obj1", "/")] (arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[0], out var str0))
                    return null;
                return (PyObject?)method.Invoke(target, [str0, arguments[1]]);
            }
            ,
            PySpecialMethodParametersType.ArgsKwargs => [PyFunctionArgsDef("*args", "**kwargs")] (arguments) =>
            {
                return (PyObject?)method.Invoke(target, [arguments.ExtraArgs, arguments.ExtraKwargs]);
            }
            ,

            _ => throw new NotSupportedException(),
        };
    }

    private static PyFunction ToPyFunction(MethodInfo method)
    {
        return [PyFunctionArgsDef("self", "*args", "**kwargs")] (arguments) =>
        {
            var (target, methodArguments) = arguments.ToMethodArguments();
            return (PyObject?)method.Invoke(target, [methodArguments]);
        };
    }

    private static PyFunction ToPyFunction(MethodInfo method, PySpecialMethodParametersType paramType)
    {
        return paramType switch
        {
            PySpecialMethodParametersType.NoArgs => [PyFunctionArgsDef("self")] (arguments) =>
            {
                return (PyObject?)method.Invoke(arguments[0], null);
            }
            ,
            PySpecialMethodParametersType.Object => [PyFunctionArgsDef("self", "obj0", "/")] (arguments) =>
            {
                return (PyObject?)method.Invoke(arguments[0], [arguments[1]]);
            }
            ,
            PySpecialMethodParametersType.String => [PyFunctionArgsDef("self", "str0", "/")] (arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[1], out var str0))
                    return null;
                return (PyObject?)method.Invoke(arguments[0], [str0]);
            }
            ,
            PySpecialMethodParametersType.ObjectObject => [PyFunctionArgsDef("self", "obj0", "obj1", "/")] (arguments) =>
            {
                return (PyObject?)method.Invoke(arguments[0], [arguments[1], arguments[2]]);
            }
            ,
            PySpecialMethodParametersType.StringObject => [PyFunctionArgsDef("self", "str0", "obj1", "/")] (arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[1], out var str0))
                    return null;
                return (PyObject?)method.Invoke(arguments[0], [str0, arguments[2]]);
            }
            ,
            PySpecialMethodParametersType.ArgsKwargs => [PyFunctionArgsDef("self", "*args", "**kwargs")] (arguments) =>
            {
                return (PyObject?)method.Invoke(arguments[0], [arguments.ExtraArgs, arguments.ExtraKwargs]);
            }
            ,

            _ => throw new NotSupportedException(),
        };


    }

}

public sealed class PyMethodDescriptorObjectType : PyPrimitiveTypeObject<PyMethodDescriptorObjectType, PyMethodDescriptorObject>
{
    public override string Name => "method_descriptor";
}