using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PySharp.Modules.Builtins;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)]
partial class PyTypeObject<TObject>
{
    internal void AppendMemberDescriptor(string name, PyMemberGetter<TObject> getter, PyMemberSetter<TObject>? setter = null, PyMemberDeleter<TObject>? deleter = null)
    {
        PyAttributes[name] = new PyMemberDescriptorObject(this, getter.ToNonGeneric(), setter?.ToNonGeneric(), deleter?.ToNonGeneric());
    }

    internal void AppendMethodDescriptor(string name, params PyMethod<TObject>[] methods)
    {
        PyAttributes.Add(name, new PyMethodDescriptorObject(name, this, PyDelegateConverter.CreateOverloadDispatcher(methods)));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillSlot<TDelegate>(string name, ref TDelegate? field, TDelegate func) where TDelegate : Delegate
    {
        field = func;
        PyAttributes.Add(name, new PyWrapperDescriptorObject(func));
    }

    protected virtual void FillSlots()
    {
    }

    protected virtual void RegisterMethods()
    {
    }

    private void AppendNew()
    {
        var newMethod = GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(method => method.Name == nameof(New) && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject));

        if (newMethod.DeclaringType == typeof(PyTypeObject<TObject>))
            return;

        Slots.New = New;

        var method = PyBuiltinFunctionOrMethodObject.CreateBoundMethodFromBound(PySpecialNames.New, this, null! /* TODO */, [PyFunctionArgsDef("cls", "*args", "**kwargs")] (context, arguments) =>
        {
            if (arguments[0] is not PyTypeObject cls)
                return PyResult.TypeError(PySR.Runtime_Type_NewClsNonType, FullName, arguments[0].PyType.FullName);

            if (!cls.IsSubclassOf(this))
                return PyResult.TypeError(PySR.Runtime_Type_NewClsNotSubtype, FullName, cls.FullName);

            if (cls.LayoutType.IsSubclassOf(LayoutType))
                return PyResult.TypeError(PySR.Runtime_Type_NewClsNotSafe, FullName, cls.FullName);
            Debug.Assert(cls.LayoutType == LayoutType || LayoutType.IsSubclassOf(cls.LayoutType));

            return New(context, cls, arguments.ExtraArgs, arguments.ExtraKwargs);
        });
        PyAttributes.Add(PySpecialNames.New, method);
    }
}
