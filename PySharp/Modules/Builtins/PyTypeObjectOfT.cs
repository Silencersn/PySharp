using PySharp.Modules.CSharp;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

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
        FillSlots();
        RegisterMethods();
        RegisterProperties();
    }

    public PyTypeObject(string qualName, IReadOnlyList<PyTypeObject> bases, bool appendOverridenMethods) : base(qualName, bases)
    {
        if (appendOverridenMethods)
        {
            FillSlots();
            RegisterMethods();
            RegisterProperties();
        }
    }

    internal sealed override PyTypeObject CreateUserDefinedTypeWithSameLayout(string name, string qualName, IReadOnlyList<PyTypeObject> bases)
    {
        return new UserDefinedType<TObject>(name, qualName, bases);
    }
}

[PyType("type")]
public sealed partial class PyTypeObjectType : PyTypeObject<PyTypeObject>
{
    protected override PyResult Call(PyCallContext context, PyTypeObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var newFunc = self.Slots.New;
        if (newFunc is null)
            return PyResult.TypeError(PySR.Runtime_Type_CannotCreateInstance, self.FullName);

        var result = newFunc(context, self, args, kwargs);
        if (result.IsError)
            return result;

        var pyObject = result.Value;
        if (self.IsInstance(pyObject))
        {
            var initFunc = self.Slots.Init;
            if (initFunc is not null)
            {
                var initResult = initFunc(context, pyObject, args, kwargs);
                if (initResult.IsError)
                    return initResult;
            }
        }

        return pyObject;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;
        return args[0].PyType;
    }

    protected override PyResult Repr(PyCallContext context, PyTypeObject self)
    {
        return PyStrObject.FromString($"<class '{self.FullName}'>");
    }

    protected override PyResult GetAttribute(PyCallContext context, PyTypeObject self, PyObject item)
    {
        return DefaultTypeGetAttribute(context, self, item);
    }

    [PyProperty(PySpecialNames.Bases)]
    private static PyResult Get_Bases(PyCallContext context, PyTypeObject self)
    {
        return PyTupleObject.CreateTuple(self.Bases);
    }

    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyTypeObject self)
    {
        return PyStrObject.FromString(self.Name);
    }

    [PyProperty(PySpecialNames.Name, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Name(PyCallContext context, PyTypeObject self, PyObject value)
    {
        if (self.IsTypeImmutable)
            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, PySpecialNames.Name, self.FullName);

        if (value is not PyStrObject str)
            return PyResult.TypeError(null);

        self.Name = str.Value;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.QualName)]
    private static PyResult Get_QualName(PyCallContext context, PyTypeObject self)
    {
        return PyStrObject.FromString(self.QualName);
    }

    [PyProperty(PySpecialNames.QualName, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_QualName(PyCallContext context, PyTypeObject self, PyObject value)
    {
        if (self.IsTypeImmutable)
            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, PySpecialNames.QualName, self.FullName);

        if (value is not PyStrObject str)
            return PyResult.TypeError(null);

        self.QualName = str.Value;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.MRO)]
    private static PyResult Get_MRO(PyCallContext context, PyTypeObject self)
    {
        return PyTupleObject.CreateTuple(self.MRO);
    }

    [PyProperty(PySpecialNames.Module)]
    private static PyResult Get_Module(PyCallContext context, PyTypeObject self)
    {
        var module = self.ModuleAsObject;
        if (module is null)
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.FullName, PySpecialNames.Module);
        return module;
    }

    [PyProperty(PySpecialNames.Module, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Module(PyCallContext context, PyTypeObject self, PyObject value)
    {
        if (self.IsTypeImmutable)
            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, PySpecialNames.Module, self.FullName);

        self.ModuleAsObject = value;
        return PyNoneObject.None;
    }
}
