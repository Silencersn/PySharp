using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Reflection;

namespace PySharp.PyRuntime.Calls;

public delegate PyResult PyFunction(PyCallContext context, PyArguments arguments);
public delegate PyResult PyMethod(PyCallContext context, PyObject self, PyArguments arguments);
public delegate PyResult PyMethod<TObject>(PyCallContext context, TObject self, PyArguments arguments) where TObject : PyObject;
public delegate PyResult PyStaticMethod(PyCallContext context, PyTypeObject cls, PyArguments arguments);
public delegate PyResult PyUncompoundedDelegate(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);
public delegate PyResult PyMemberGetter(PyCallContext context, PyObject self);
public delegate PyResult PyMemberGetter<TObject>(PyCallContext context, TObject self) where TObject : PyObject;
public delegate PyResult PyMemberSetter(PyCallContext context, PyObject self, PyObject value);
public delegate PyResult PyMemberSetter<TObject>(PyCallContext context, TObject self, PyObject value) where TObject : PyObject;
public delegate PyResult PyMemberDeleter(PyCallContext context, PyObject self);
public delegate PyResult PyMemberDeleter<TObject>(PyCallContext context, TObject self) where TObject : PyObject;

public delegate PyResult PyUnaryFunction(PyCallContext context, PyObject self);
public delegate PyResult PyBinaryFunction(PyCallContext context, PyObject self, PyObject other);
public delegate PyResult PyTernaryFunction(PyCallContext context, PyObject self, PyObject other, PyObject third);

public static class PyDelegateConverter
{
    public static PyMemberGetter ToNonGeneric<TObject>(this PyMemberGetter<TObject> getter) where TObject : PyObject
    {
        return (context, self) =>
        {
            if (self is not TObject selfOfT)
                return PyResult.RaiseTypeError(null);

            return getter(context, selfOfT);
        };
    }
    public static PyMemberSetter ToNonGeneric<TObject>(this PyMemberSetter<TObject> setter) where TObject : PyObject
    {
        return (context, self, value) =>
        {
            if (self is not TObject selfOfT)
                return PyResult.RaiseTypeError(null);

            return setter(context, selfOfT, value);
        };
    }
    public static PyMemberDeleter ToNonGeneric<TObject>(this PyMemberDeleter<TObject> deleter) where TObject : PyObject
    {
        return (context, self) =>
        {
            if (self is not TObject selfOfT)
                return PyResult.RaiseTypeError(null);

            return deleter(context, selfOfT);
        };
    }

    public static PyUncompoundedDelegate CreateOverloadDispatcher<TObject>(params PyMethod<TObject>[] methods) where TObject : PyObject
    {
        PyArgsDef[]? defs = null;

        return (context, args, kwargs) =>
        {
            if (args.Count is 0 || args[0] is not TObject selfOfT)
                return PyResult.RaiseTypeError(null);

            EnsureDefCache();
            Debug.Assert(defs is not null);

            args = [.. args.Skip(1)];
            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i].TryParse(args, kwargs, out var result))
                    return methods[i].Invoke(context, selfOfT, result);
            }

            return PyResult.RaiseTypeError(null);
        };

        void EnsureDefCache()
        {
            if (defs is not null)
                return;

            lock (methods)
            {
                if (defs is not null)
                    return;

                var cache = new PyArgsDef[methods.Length];
                for (int i = 0; i < cache.Length; i++)
                {
                    var argsDef = methods[i].Method.GetCustomAttribute<PyFunctionArgsDefAttribute>();
                    Debug.Assert(argsDef is not null);
                    cache[i] = PyArgsDef.FromDef(argsDef.Parameters);
                }
                defs = cache;
            }
        }
    }
    public static PyUncompoundedDelegate CreateOverloadDispatcher(params PyFunction[] functions)
    {
        PyArgsDef[]? defs = null;

        return (context, args, kwargs) =>
        {
            EnsureDefCache();
            Debug.Assert(defs is not null);

            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i].TryParse(args, kwargs, out var result))
                    return functions[i].Invoke(context, result);
            }

            return PyResult.RaiseTypeError(null);
        };

        void EnsureDefCache()
        {
            if (defs is not null)
                return;

            lock (functions)
            {
                if (defs is not null)
                    return;

                var cache = new PyArgsDef[functions.Length];
                for (int i = 0; i < cache.Length; i++)
                {
                    var argsDef = functions[i].Method.GetCustomAttribute<PyFunctionArgsDefAttribute>();
                    Debug.Assert(argsDef is not null);
                    cache[i] = PyArgsDef.FromDef(argsDef.Parameters);
                }
                defs = cache;
            }
        }
    }

    public static PyUnaryFunction ToUnaryFunction(this PyObject obj)
    {
        return (context, self) => obj.Call(context, [self], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyBinaryFunction ToBinaryFunction(this PyObject obj)
    {
        return (context, self, other) => obj.Call(context, [self, other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyTernaryFunction ToTernaryFunction(this PyObject obj)
    {
        return (context, self, other, third) => obj.Call(context, [self, other, third], FrozenDictionary<string, PyObject>.Empty);
    }
}