using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Runtime.CompilerServices;

namespace PySharp.Modules.Threading;

/// <summary>
/// threading.local (_thread._local) — thread-local attribute storage: every
/// thread starts from an empty dict (the creating thread's attributes do
/// NOT carry over), and a subclass __init__ runs again in each thread the
/// first time the local is touched there, with the construction arguments.
/// </summary>
public sealed partial class PyLocalObject : PyObjectManagedDict
{
    private readonly ConditionalWeakTable<Thread, PyDictObject> _perThread = [];

    // the arguments threading.local(*args, **kwargs) was constructed with;
    // a subclass __init__ replays them in every fresh thread
    internal readonly IReadOnlyList<PyObject> _initArgs;
    internal readonly IReadOnlyDictionary<string, PyObject> _initKwargs;

    public override PyTypeObject DefaultPyType => PyLocalObjectType.Shared;

    internal PyLocalObject(IReadOnlyList<PyObject> initArgs, IReadOnlyDictionary<string, PyObject> initKwargs)
    {
        _initArgs = initArgs;
        _initKwargs = initKwargs;
    }

    // the creating thread's dict is registered during construction so the
    // __init__ that type(...) runs right after __new__ writes into it;
    // without this, that first write would look like a fresh thread's
    // first touch and replay __init__ on top of it (CPython local_new
    // creates the dict before calling tp_init)
    internal void SeedCreatingThreadAttributes() =>
        _perThread.GetValue(Thread.CurrentThread, static _ => new PyDictObject());

    // the per-thread dict behind the instance attribute machinery; pure
    // dictionary lookup, the first-touch __init__ replay happens in the
    // attribute slots below where a context is available
    internal override IPyAttributesObject PyAttributes
    {
        get => _perThread.GetValue(Thread.CurrentThread, static _ => new PyDictObject());
    }

    // returns the current thread's dict, creating it (and replaying a
    // subclass __init__) on that thread's first touch of the local
    internal PyResult EnsureThreadAttributes(PyCallContext context)
    {
        var thread = Thread.CurrentThread;
        if (_perThread.TryGetValue(thread, out var existing))
            return existing;

        var dict = new PyDictObject();
        _perThread.GetValue(thread, _ => dict);

        // CPython only replays __init__ for subclasses: the plain local
        // has no __init__ to run
        if (!ReferenceEquals(PyType, PyLocalObjectType.Shared))
        {
            var replayed = this.CallMethod(context, PySpecialNames.Init, _initArgs, _initKwargs);
            if (replayed.IsError)
                return replayed;
        }
        return dict;
    }
}

[PyType("_local", Module = "_thread")]
public sealed partial class PyLocalObjectType : PyTypeObject<PyLocalObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("*args", "**kwargs")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        Dictionary<string, PyObject> kwargs = [];
        foreach (var pair in arguments.ExtraKwargs)
            kwargs[pair.Key] = pair.Value;

        return new PyLocalObject([.. arguments.ExtraArgs], kwargs);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // the base local rejects construction arguments outright
        // (_thread._local); a subclass keeps them for its __init__ replays
        if (ReferenceEquals(cls, Shared) && (args.Count is not 0 || kwargs.Count is not 0))
            return PyResult.RaiseException(PyTypeErrorObjectType.Shared, PySR.Runtime_Threading_LocalInitArguments);

        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        var local = (PyLocalObject)obj.Value;
        local._pyType = cls;
        local.SeedCreatingThreadAttributes();
        return obj;
    }

    protected override PyResult GetAttribute(PyCallContext context, PyLocalObject self, PyObject item)
    {
        var ensured = self.EnsureThreadAttributes(context);
        if (ensured.IsError)
            return ensured;
        return base.GetAttribute(context, self, item);
    }

    protected override PyResult SetAttr(PyCallContext context, PyLocalObject self, PyObject key, PyObject value)
    {
        var ensured = self.EnsureThreadAttributes(context);
        if (ensured.IsError)
            return ensured;
        return base.SetAttr(context, self, key, value);
    }

    protected override PyResult DelAttr(PyCallContext context, PyLocalObject self, PyObject item)
    {
        var ensured = self.EnsureThreadAttributes(context);
        if (ensured.IsError)
            return ensured;
        return base.DelAttr(context, self, item);
    }
}
