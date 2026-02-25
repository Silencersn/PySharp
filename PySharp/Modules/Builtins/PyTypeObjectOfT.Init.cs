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
        var uncompoundedDelegate = methods.Length is 1 ? methods[0].ToUncompounded() : PyDelegateConverter.CreateOverloadDispatcher(methods);
        PyAttributes.Add(name, new PyMethodDescriptorObject(name, this, uncompoundedDelegate));
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

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillNewSlot()
    {
        Slots.New = (context, cls, args, kwargs) =>
        {
            var validateResult = PyArgsValidator.ValidateNewCls(this, cls);
            if (validateResult.IsError)
                return validateResult;

            return New(context, cls, args, kwargs);
        };

        var method = PyBuiltinFunctionOrMethodObject.CreateBoundMethodFromBound(PySpecialNames.New, this, null! /* TODO */, (context, args, kwargs) =>
        {
            if (args.Count is 0)
                return PyResult.TypeError(null /* TODO */);

            if (args[0] is not PyTypeObject cls)
                return PyResult.TypeError(PySR.Runtime_Type_NewClsNonType, FullName, args[0].PyType.FullName);

            var validateResult = PyArgsValidator.ValidateNewCls(this, cls);
            if (validateResult.IsError)
                return validateResult;

            return New(context, cls, [.. args.Skip(1)], kwargs);
        });
        PyAttributes.Add(PySpecialNames.New, method);
    }
}
