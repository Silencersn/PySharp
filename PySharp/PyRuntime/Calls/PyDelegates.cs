using PySharp.PyModules.Builtins;

namespace PySharp.PyRuntime.Calls;

public delegate PyResult PyFunction(PyCallContext context, PyArguments arguments);
public delegate PyResult PyMethod(PyCallContext context, PyObject self, PyArguments arguments);
public delegate PyResult PyMethod<TObject>(PyCallContext context, TObject self, PyArguments arguments) where TObject : PyObject;
public delegate PyResult PyStaticMethod(PyCallContext context, PyTypeObject cls, PyArguments arguments);
public delegate PyResult PyUncompoundedDelegate(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);