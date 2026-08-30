using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Warnings;

// PEP 702 decorator: warns at runtime when the decorated class/function is used.
public sealed class PyDeprecatedObject : PyObject
{
    internal string Message { get; }
    internal PyTypeObject<PyExceptionObject>? Category { get; }
    internal int Stacklevel { get; }

    internal PyDeprecatedObject(string message, PyTypeObject<PyExceptionObject>? category, int stacklevel)
    {
        Message = message;
        Category = category;
        Stacklevel = stacklevel;
    }

    public override PyTypeObject DefaultPyType => PyDeprecatedObjectType.Shared;

    internal PyResult Apply(PyCallContext context, IReadOnlyList<PyObject> args)
    {
        if (args.Count is not 1)
            return PyResult.TypeError($"@deprecated decorator expects exactly one argument, got {args.Count}");
        var arg = args[0];

        // category=None: only tag the object, never warn.
        if (Category is null)
        {
            var setDep = PyOperators.SetAttr(context, arg, "__deprecated__", PyStrObject.FromString(Message));
            return setDep.IsError ? setDep : arg;
        }

        if (arg is PyTypeObject typeArg)
            return ApplyToClass(context, typeArg);
        if (arg.PyType.Slots.Call is not null)
            return ApplyToCallable(context, arg);
        return PyResult.TypeError($"[deprecated] must be applied to a class or callable, not {arg.PyType.Name}");
    }

    private PyResult ApplyToCallable(PyCallContext context, PyObject original)
    {
        var dep = PyStrObject.FromString(Message);
        var wrapper = new PyDeprecatedWrapperObject(original, this);

        var setWrapperDep = PyOperators.SetAttr(context, wrapper, "__deprecated__", dep);
        if (setWrapperDep.IsError)
            return setWrapperDep;
        if (original is PyObjectManagedDict)
        {
            var setOriginalDep = PyOperators.SetAttr(context, original, "__deprecated__", dep);
            if (setOriginalDep.IsError)
                return setOriginalDep;
        }
        return wrapper;
    }

    private PyResult ApplyToClass(PyCallContext context, PyTypeObject cls)
    {
        var message = PyStrObject.FromString(Message);

        // Wrap __new__ so instantiating the decorated class warns once.
        var originalNew = PyOperators.GetAttr(context, cls, "__new__");
        var newWrapper = PyBuiltinFunctionOrMethodObject.CreateFunction("__new__", (ctx, callArgs, callKwargs) =>
        {
            if (callArgs.Count > 0 && ReferenceEquals(callArgs[0], cls))
            {
                var warnResult = ctx.Warn(message, Category, Stacklevel + 1);
                if (warnResult.IsError)
                    return warnResult;
            }

            if (originalNew.IsError)
                return PyResult.TypeError($"{cls.Name} has no usable __new__");
            // original.__new__(cls, *args, **kwargs): the New slot passes cls as callArgs[0].
            return originalNew.Value.Call(ctx, callArgs, callKwargs);
        });

        // Set __new__ directly (not via staticmethod) so TrySetSlot wires the New slot
        // to a callable that receives (cls, *args) without a staticmethod indirection.
        var setNew = PyOperators.SetAttr(context, cls, "__new__", newWrapper);
        if (setNew.IsError)
            return setNew;

        // Wrap __init_subclass__ so creating a subclass warns.
        var originalInitSubclass = PyOperators.GetAttr(context, cls, "__init_subclass__");
        var initSubclassWrapper = PyBuiltinFunctionOrMethodObject.CreateFunction("__init_subclass__", (ctx, callArgs, callKwargs) =>
        {
            var warnResult = ctx.Warn(message, Category, Stacklevel + 1);
            if (warnResult.IsError)
                return warnResult;
            if (originalInitSubclass.IsError)
                return originalInitSubclass;
            // The classmethod descriptor binds cls, so only pass the keyword args through.
            return originalInitSubclass.Value.Call(ctx, [], callKwargs);
        });
        var setInit = PyOperators.SetAttr(context, cls, "__init_subclass__", new PyClassMethodObject(initSubclassWrapper));
        if (setInit.IsError)
            return setInit;

        var setDep = PyOperators.SetAttr(context, cls, "__deprecated__", message);
        if (setDep.IsError)
            return setDep;

        return cls;
    }
}

[PyType("warnings.deprecated")]
public sealed partial class PyDeprecatedObjectType : PyTypeObject<PyDeprecatedObject>
{
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count is 0)
            return PyResult.TypeError("deprecated() missing required argument: 'message'");
        if (args.Count > 1)
            return PyResult.TypeError($"deprecated() takes from 1 to 1 positional arguments but {args.Count} were given");
        if (args[0] is not PyStrObject messageObj)
            return PyResult.TypeError($"Expected an object of type str for 'message', not '{args[0].PyType.Name}'");

        PyTypeObject<PyExceptionObject>? category = PyDeprecationWarningObjectType.Shared;
        if (kwargs.TryGetValue("category", out var catObj))
        {
            if (catObj is PyNoneObject)
                category = null;
            else if (catObj is not PyTypeObject<PyExceptionObject> cat || !cat.IsSubclassOf(PyWarningObjectType.Shared))
                return PyResult.TypeError($"category must be a Warning subclass, not '{catObj.PyType.Name}'");
            else
                category = cat;
        }

        int stacklevel = 1;
        if (kwargs.TryGetValue("stacklevel", out var slObj))
        {
            var idx = PySpecialMethods.Index(context, slObj);
            if (idx.IsError)
                return idx.ExceptionResult;
            if (!idx.Value.IsInt32)
                return PyResult.OverflowError("stacklevel is too large");
            stacklevel = idx.Value.Int32Value;
        }

        return new PyDeprecatedObject(messageObj.Value, category, stacklevel);
    }

    protected override PyResult Call(PyCallContext context, PyDeprecatedObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self.Apply(context, args);
    }
}

// A dict-backed, callable wrapper returned when decorating a callable. Storing the original
// callable and the owner lets the wrapper warn then forward the call, while still supporting
// instance attributes such as __deprecated__ (unlike builtin function objects).
public sealed class PyDeprecatedWrapperObject : PyObjectManagedDict
{
    internal PyObject Original { get; }
    internal PyDeprecatedObject Owner { get; }

    internal PyDeprecatedWrapperObject(PyObject original, PyDeprecatedObject owner)
    {
        Original = original;
        Owner = owner;
    }

    public override PyTypeObject DefaultPyType => PyDeprecatedWrapperObjectType.Shared;
}

[PyType("warnings._deprecated_wrapper")]
public sealed partial class PyDeprecatedWrapperObjectType : PyTypeObject<PyDeprecatedWrapperObject>
{
    protected override PyResult Call(PyCallContext context, PyDeprecatedWrapperObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var warnResult = context.Warn(PyStrObject.FromString(self.Owner.Message), self.Owner.Category, self.Owner.Stacklevel + 1);
        if (warnResult.IsError)
            return warnResult;
        return self.Original.Call(context, args, kwargs);
    }
}
