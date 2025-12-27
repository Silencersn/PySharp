using PySharp.PyModules.Builtins;

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
}