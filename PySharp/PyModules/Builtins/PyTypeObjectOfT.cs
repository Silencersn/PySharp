using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

/// <summary>
/// 
/// </summary>
/// <typeparam name="TObject">
/// This generic parameter is intended solely for storing data, similar to how CPython uses memory layouts.
/// In general, any type derived from PyObject can be used as TObject.
/// However, in this Python implementation, each standard library data class is paired one-to-one with its corresponding type class.
/// If you define a non-standard type in C# using a standard library data class as TObject
/// (e.g., MyType : PyTypeObject&lt;PyIntObject&gt;), some internal details in the standard library will still treat it as an int.
/// Please exercise caution when choosing a standard library data class as this generic parameter.
/// </typeparam>
public abstract partial class PyTypeObject<TObject> : PyTypeObject where TObject : PyObject
{
    public sealed override Type LayoutType => typeof(TObject);
    public override PyTypeObject DefaultPyType => PyTypeObjectType.Shared;

    public PyTypeObject()
    {
        AppendOverridenSpecialMethodDescriptors();
        AppendNew();
    }

    public PyTypeObject(string name, IReadOnlyList<PyTypeObject> bases, bool appendOverridenMethods) : base(name, bases)
    {
        if (appendOverridenMethods)
        {
            AppendOverridenSpecialMethodDescriptors();
            AppendNew();
        }
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError($"cannot create '{Name}' instances");
    }
}

public sealed class PyTypeObjectType : PyTypeObject<PyTypeObjectType, PyTypeObject>
{
    public override string Name => "type";

    public PyTypeObjectType()
    {
        AppendMemberDescriptor(PySpecialNames.Bases,
            static (_, typeObj) => PyTupleObject.CreateTuple(typeObj.Bases),
            static (_, typeObj, value) => throw new NotImplementedException());

        AppendMemberDescriptor(PySpecialNames.Name,
            static (_, typeObj) => PyStrObject.FromString(typeObj.Name),
            static (_, typeObj, value) => throw new NotImplementedException());

        AppendMemberDescriptor(PySpecialNames.MRO,
            static (_, typeObj) => PyTupleObject.CreateTuple(typeObj.MRO),
            static (_, typeObj, value) => throw new NotImplementedException());
    }

    protected internal override PyResult Call(PyCallContext context, PyTypeObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var result = self.New(context, self, args, kwargs);
        if (result.IsError)
            return result;

        var pyObject = result.Value;
        if (self.IsInstance(pyObject))
        {
            var initResult = self.Init(context, pyObject, args, kwargs);
            if (initResult.IsError)
                return initResult;
        }

        return pyObject;
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;
        return args[0].PyType;
    }

    protected internal override PyResult Repr(PyCallContext context, PyTypeObject self)
    {
        return PyStrObject.FromString($"<class '{self.Name}'>");
    }

    protected internal override PyResult GetAttribute(PyCallContext context, PyTypeObject self, string item)
    {
        return PyTypeGetAttribute(context, self, item);
    }
}

public abstract class PyTypeObject<TSelf, TObject> : PyTypeObject<TObject>
    where TSelf : PyTypeObject<TSelf, TObject>, new()
    where TObject : PyObject
{
    public static TSelf Shared { get; } = new();
}