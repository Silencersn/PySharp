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
    private readonly PyTypeObject _declaringType;
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

    public override PyTypeObject DefaultPyType => PyMethodDescriptorObjectType.Shared;

    bool IPyDescriptor.SupportsGet => true;

    bool IPyDescriptor.SupportsSet => false;

    bool IPyDescriptor.SupportsDelete => false;

    internal PyMethodDescriptorObject(string name, PyTypeObject declaringType, MethodInfo method, PySpecialMethodParametersType paramType)
    {
        _declaringType = declaringType;
        _name = name;
        _method = method;
        _paramType = paramType;
    }
    internal PyMethodDescriptorObject(string name, PyTypeObject declaringType, IEnumerable<MethodInfo> methods)
    {
        _declaringType = declaringType;
        _name = name;
        _methods = [.. methods];
    }

    protected internal override PyObject? GetImpl(PyObject instance, PyObject owner)
    {
        if (owner is not PyTypeObject pyType)
            return PyVirtualMachine.RaiseTypeError(null);

        if (instance is PyNoneObject)
            return this;

        // class Demo:
        //     __add__ = int.__add__
        //
        // demo = Demo()
        // value = demo + 0
        //
        // must check the type
        // if not, int.__add__ will be bound to the demo incorrectly
        // however, int.__add__ is a virtual call of PyObject in C#
        // so, it actually will call the Add of the type which is subclass of PyObject for supporting custom python types
        // as a result, demo.__add__ calls it self
        // leading to a StackOverflowException in C#
        // 
        if (!_declaringType.IsInstance(instance))
            return PyVirtualMachine.RaiseTypeError($"descriptor '{_name}' requires a '{_declaringType.Name}' object but received a '{instance.PyType.Name}'");

        if (_methods is not null)
            return new PyBuiltinFunctionOrMethodObject(_name, instance, pyType, [.. _methods.Select(method => ToPyFunctionDirectly(method, instance))]);

        Debug.Assert(_method is not null);
        Debug.Assert(_paramType is not PySpecialMethodParametersType.Unknown);
        if (_method.IsStatic)
            return new PyBuiltinFunctionOrMethodObject(_name, instance, pyType, ToPyFunctionFromStaticMethod(_method, instance, _paramType));
        return new PyBuiltinFunctionOrMethodObject(_name, instance, pyType, ToPyFunction(_method, instance, _paramType));
    }

    protected internal override PyObject? CallImpl(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is 0)
            return PyVirtualMachine.RaiseTypeError($"descriptor '{_name}' of '{_declaringType.Name}' object needs an argument");

        if (!_declaringType.IsInstance(args[0]))
            return PyVirtualMachine.RaiseTypeError($"descriptor '{_name}' requires a '{_declaringType.Name}' object but received a '{args[0].PyType.Name}'");

        return UnboundMethod.Call(args, kwargs);
    }

    private static PyFunction ToPyFunctionDirectly(MethodInfo method, PyObject target)
    {
        return method.CreateDelegate<PyFunction>(target);
    }

    private static PyFunction ToPyFunction(MethodInfo method, PyObject target, PySpecialMethodParametersType paramType)
    {
        Debug.Assert(!method.IsStatic);
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

    private static PyFunction ToPyFunctionFromStaticMethod(MethodInfo method, PyObject target, PySpecialMethodParametersType paramType)
    {
        Debug.Assert(method.IsStatic);
        return paramType switch
        {
            PySpecialMethodParametersType.NoArgs => [PyFunctionArgsDef()] (arguments) =>
            {
                return (PyObject?)method.Invoke(null, [target]);
            }
            ,
            PySpecialMethodParametersType.Object => [PyFunctionArgsDef("obj0", "/")] (arguments) =>
            {
                return (PyObject?)method.Invoke(null, [target, arguments[0]]);
            }
            ,
            PySpecialMethodParametersType.String => [PyFunctionArgsDef("str0", "/")] (arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[0], out var str0))
                    return null;
                return (PyObject?)method.Invoke(null, [target, str0]);
            }
            ,
            PySpecialMethodParametersType.ObjectObject => [PyFunctionArgsDef("obj0", "obj1", "/")] (arguments) =>
            {
                return (PyObject?)method.Invoke(null, [target, arguments[0], arguments[1]]);
            }
            ,
            PySpecialMethodParametersType.StringObject => [PyFunctionArgsDef("str0", "obj1", "/")] (arguments) =>
            {
                if (!Utils.TryCastStrAsArg(arguments[0], out var str0))
                    return null;
                return (PyObject?)method.Invoke(null, [target, str0, arguments[1]]);
            }
            ,
            PySpecialMethodParametersType.ArgsKwargs => [PyFunctionArgsDef("*args", "**kwargs")] (arguments) =>
            {
                return (PyObject?)method.Invoke(null, [target, arguments.ExtraArgs, arguments.ExtraKwargs]);
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