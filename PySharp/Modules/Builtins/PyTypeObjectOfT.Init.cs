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
        PyAttributes[name] = new PyMethodDescriptorObject(name, this, uncompoundedDelegate);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendMethodDescriptor(string name, params PyDelegateDefinition<PyMethod<TObject>>[] methods)
    {
        var uncompoundedDelegate = methods.Length is 1 ? methods[0].ToUncompounded() : PyDelegateConverter.CreateOverloadDispatcher(methods);
        PyAttributes[name] = new PyMethodDescriptorObject(name, this, uncompoundedDelegate);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendClassMethod(string name, PyDelegateDefinition<PyMethod<PyTypeObject>> classMethod)
    {
        var uncompoundedDelegate = classMethod.ToUncompounded();
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, classMethod.ToUncompounded());
        PyAttributes[name] = new PyClassMethodObject(func);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendClassMethod(string name, params PyDelegateDefinition<PyMethod<PyTypeObject>>[] classMethods)
    {
        var uncompoundedDelegate = classMethods.Length is 1 ? classMethods[0].ToUncompounded() : PyDelegateConverter.CreateOverloadDispatcher(classMethods);
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, uncompoundedDelegate);
        PyAttributes[name] = new PyClassMethodObject(func);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendStaticMethod(string name, PyDelegateDefinition<PyFunction> staticMethod)
    {
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, staticMethod.ToUncompounded());
        PyAttributes[name] = new PyStaticMethodObject(func);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendStaticMethod(string name, params PyDelegateDefinition<PyFunction>[] staticMethods)
    {
        var uncompoundedDelegate = staticMethods.Length is 1 ? staticMethods[0].ToUncompounded() : PyDelegateConverter.CreateOverloadDispatcher(staticMethods);
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, uncompoundedDelegate);
        PyAttributes[name] = new PyStaticMethodObject(func);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillSlot<TDelegate>(string name, ref TDelegate? field, TDelegate func) where TDelegate : Delegate
    {
        field = func;
        PyAttributes[name] = new PyWrapperDescriptorObject(func);
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
        PyAttributes[PySpecialNames.New] = method;
    }
}
