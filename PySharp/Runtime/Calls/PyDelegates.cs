using PySharp.Modules.Builtins;
using System.Diagnostics;

namespace PySharp.Runtime.Calls;

public delegate PyResult PyFunction(PyCallContext context, PyArguments arguments);
public delegate PyResult PyMethod(PyCallContext context, PyObject self, PyArguments arguments);
public delegate PyResult PyMethod<TObject>(PyCallContext context, TObject self, PyArguments arguments) where TObject : PyObject;
public delegate PyResult PyUncompoundedDelegate(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);
public delegate PyResult PyMemberGetter(PyCallContext context, PyObject self);
public delegate PyResult PyMemberGetter<TObject>(PyCallContext context, TObject self) where TObject : PyObject;
public delegate PyResult PyMemberSetter(PyCallContext context, PyObject self, PyObject value);
public delegate PyResult PyMemberSetter<TObject>(PyCallContext context, TObject self, PyObject value) where TObject : PyObject;
public delegate PyResult PyMemberDeleter(PyCallContext context, PyObject self);
public delegate PyResult PyMemberDeleter<TObject>(PyCallContext context, TObject self) where TObject : PyObject;

public delegate PyResult PyUnaryFunction(PyCallContext context, PyObject self);
public delegate PyResult PyBinaryFunction(PyCallContext context, PyObject self, PyObject other);
public delegate PyResult PyTernaryFunction(PyCallContext context, PyObject self, PyObject second, PyObject third);
public delegate PyResult PyQuaternaryFunction(PyCallContext context, PyObject self, PyObject second, PyObject third, PyObject fourth);
public delegate PyResult PySelfArgsKwargsFunction(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);
public delegate PyResult PyClsArgsKwargsFunction(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);

// Buffer protocol delegates
public delegate PyResult PyBufferFunction(PyCallContext context, PyObject self, int flags);
public delegate PyResult PyReleaseBufferFunction(PyCallContext context, PyObject self, PyObject buffer);

public readonly struct PyDelegateDefinition<T> where T : Delegate
{
    public readonly T Delegate;
    public readonly string[] Parameters;

    public PyDelegateDefinition(T @delegate, string[] parameters)
    {
        Delegate = @delegate;
        Parameters = parameters;
    }
}

public static class PyDelegateConverter
{
    public static PyMemberGetter ToNonGeneric<TObject>(this PyMemberGetter<TObject> getter) where TObject : PyObject
    {
        return (context, self) =>
        {
            if (self is not TObject selfOfT)
                return PyResult.TypeError(null);

            return getter(context, selfOfT);
        };
    }
    public static PyMemberSetter ToNonGeneric<TObject>(this PyMemberSetter<TObject> setter) where TObject : PyObject
    {
        return (context, self, value) =>
        {
            if (self is not TObject selfOfT)
                return PyResult.TypeError(null);

            return setter(context, selfOfT, value);
        };
    }
    public static PyMemberDeleter ToNonGeneric<TObject>(this PyMemberDeleter<TObject> deleter) where TObject : PyObject
    {
        return (context, self) =>
        {
            if (self is not TObject selfOfT)
                return PyResult.TypeError(null);

            return deleter(context, selfOfT);
        };
    }

    public static PyUncompoundedDelegate ToUncompounded(this PyDelegateDefinition<PyFunction> method)
    {
        PyArgsDef? def = null;

        return (context, args, kwargs) =>
        {
            def ??= PyArgsDef.FromDef(method.Parameters);

            using var buffer = def.CreateBuffer();
            if (def.TryParse(args, kwargs, buffer, out var result))
                return method.Delegate.Invoke(context, result);

            return PyResult.TypeError(null);
        };
    }
    public static PyUncompoundedDelegate ToUncompounded<TObject>(this PyDelegateDefinition<PyMethod<TObject>> method) where TObject : PyObject
    {
        PyArgsDef? def = null;

        return (context, args, kwargs) =>
        {
            if (args.Count is 0 || args[0] is not TObject selfOfT)
                return PyResult.TypeError(null);

            def ??= PyArgsDef.FromDef(method.Parameters);

            args = [.. args.Skip(1)];
            using var buffer = def.CreateBuffer();
            if (def.TryParse(args, kwargs, buffer, out var result))
                return method.Delegate.Invoke(context, selfOfT, result);

            return PyResult.TypeError(null);
        };
    }

    public static PyUncompoundedDelegate CreateOverloadDispatcher(params PyDelegateDefinition<PyFunction>[] functions)
    {
        PyArgsDef[]? defs = null;

        return (context, args, kwargs) =>
        {
            EnsureDefCache();
            Debug.Assert(defs is not null);

            for (int i = 0; i < defs.Length; i++)
            {
                using var buffer = defs[i].CreateBuffer();
                if (defs[i].TryParse(args, kwargs, buffer, out var result))
                    return functions[i].Delegate.Invoke(context, result);
            }

            return PyResult.TypeError(null);
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
                    cache[i] = PyArgsDef.FromDef(functions[i].Parameters);
                defs = cache;
            }
        }
    }
    public static PyUncompoundedDelegate CreateOverloadDispatcher<TObject>(params PyDelegateDefinition<PyMethod<TObject>>[] methods) where TObject : PyObject
    {
        PyArgsDef[]? defs = null;

        return (context, args, kwargs) =>
        {
            if (args.Count is 0 || args[0] is not TObject selfOfT)
                return PyResult.TypeError(null);

            EnsureDefCache();
            Debug.Assert(defs is not null);

            args = [.. args.Skip(1)];
            for (int i = 0; i < defs.Length; i++)
            {
                using var buffer = defs[i].CreateBuffer();
                if (defs[i].TryParse(args, kwargs, buffer, out var result))
                    return methods[i].Delegate.Invoke(context, selfOfT, result);
            }

            return PyResult.TypeError(null);
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
                    cache[i] = PyArgsDef.FromDef(methods[i].Parameters);
                defs = cache;
            }
        }
    }

    public static PyUnaryFunction ToUnaryFunction(this PyObject obj)
    {
        return (context, self) => obj.Call(context, [self]);
    }
    public static PyBinaryFunction ToBinaryFunction(this PyObject obj)
    {
        return (context, self, other) => obj.Call(context, [self, other]);
    }
    public static PyTernaryFunction ToTernaryFunction(this PyObject obj)
    {
        return (context, self, other, third) => obj.Call(context, [self, other, third]);
    }
    public static PyQuaternaryFunction ToQuaternaryFunction(this PyObject obj)
    {
        return (context, self, other, third, fourth) => obj.Call(context, [self, other, third, fourth]);
    }
    public static PySelfArgsKwargsFunction ToSelfArgsKwargsFunction(this PyObject obj)
    {
        return (context, self, args, kwargs) => obj.Call(context, [self, .. args], kwargs);
    }
    public static PyClsArgsKwargsFunction ToClsArgsKwargsFunction(this PyObject obj)
    {
        return (context, cls, args, kwargs) => obj.Call(context, [cls, .. args], kwargs);
    }

    public static PyBufferFunction ToBufferFunction(this PyObject obj)
    {
        return (context, self, flags) => obj.Call(context, [self, PyIntObject.FromInteger(flags)]);
    }

    public static PyReleaseBufferFunction ToReleaseBufferFunction(this PyObject obj)
    {
        return (context, self, buffer) => obj.Call(context, [self, buffer]);
    }
}