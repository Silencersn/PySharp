using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)]
partial class PyTypeObject<TObject>
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendMemberDescriptor(string name, PyMemberGetter<TObject> getter, PyMemberSetter<TObject>? setter = null, PyMemberDeleter<TObject>? deleter = null)
    {
        PyAttributes[name] = new PyMemberDescriptorObject(this, getter.ToNonGeneric(), setter?.ToNonGeneric(), deleter?.ToNonGeneric());
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendMethodDescriptor(string name, PyDelegateDefinition<PyMethod<TObject>> method)
    {
        var uncompoundedDelegate = method.ToUncompounded();
        PyAttributes.Add(name, new PyMethodDescriptorObject(name, this, uncompoundedDelegate));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendMethodDescriptor(string name, params PyDelegateDefinition<PyMethod<TObject>>[] methods)
    {
        var uncompoundedDelegate = methods.Length is 1 ? methods[0].ToUncompounded() : PyDelegateConverter.CreateOverloadDispatcher(methods);
        PyAttributes.Add(name, new PyMethodDescriptorObject(name, this, uncompoundedDelegate));
    }


    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendClassMethod(string name, PyClassMethod classMethod)
    {
        var uncompoundedDelegate = classMethod.ToUncompounded();
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, classMethod.ToUncompounded());
        PyAttributes.Add(name, new PyClassMethodObject(func));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendClassMethod(string name, params PyClassMethod[] classMethods)
    {
        var uncompoundedDelegate = classMethods.Length is 1 ? classMethods[0].ToUncompounded() : PyDelegateConverter.CreateOverloadDispatcher(classMethods);
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, uncompoundedDelegate);
        PyAttributes.Add(name, new PyClassMethodObject(func));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendStaticMethod(string name, PyFunction staticMethod)
    {
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, staticMethod);
        PyAttributes.Add(name, new PyStaticMethodObject(func));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendStaticMethod(string name, params PyFunction[] staticMethods)
    {
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, staticMethods);
        PyAttributes.Add(name, new PyStaticMethodObject(func));
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

    protected virtual void RegisterProperties()
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
