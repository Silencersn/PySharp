using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

public static partial class PyThreadingFunctions
{
    // threading.py's RLock is a factory function, not the _thread.RLock
    // type itself; the arguments are accepted (with a deprecation path)
    // and ignored
    [PyExport("RLock", nameof(RLockImpl))]
    public static partial PyBuiltinFunctionOrMethodObject RLock { get; }

    [PyFunctionParameters("*args", "**kwargs")]
    private static PyResult RLockImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments.ExtraArgs.Count is not 0 || arguments.ExtraKwargs.Count is not 0)
        {
            var warnResult = context.Warn(
                PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedRLockArguments);
            if (warnResult.IsError)
                return warnResult;
        }

        return new PyRLockObject();
    }

    // _thread.get_ident: PySharp reports the managed thread id, the same
    // value Thread.ident carries
    [PyExport("get_ident", nameof(GetIdentImpl))]
    public static partial PyBuiltinFunctionOrMethodObject GetIdent { get; }

    [PyFunctionParameters()]
    private static PyResult GetIdentImpl(PyCallContext context, PyArguments arguments)
    {
        return PyIntObject.FromInteger(Environment.CurrentManagedThreadId);
    }

    // on Windows CPython reports the same kernel thread id for ident and
    // native_id; the managed id plays both roles here too
    [PyExport("get_native_id", nameof(GetNativeIdImpl))]
    public static partial PyBuiltinFunctionOrMethodObject GetNativeId { get; }

    [PyFunctionParameters()]
    private static PyResult GetNativeIdImpl(PyCallContext context, PyArguments arguments)
    {
        return PyIntObject.FromInteger(Environment.CurrentManagedThreadId);
    }

    [PyExport("current_thread", nameof(CurrentThreadImpl))]
    public static partial PyBuiltinFunctionOrMethodObject CurrentThread { get; }

    [PyFunctionParameters()]
    private static PyResult CurrentThreadImpl(PyCallContext context, PyArguments arguments)
    {
        // a thread outside this module's control gets the synthetic dummy
        // object, registered so repeated calls agree (threading.py
        // current_thread)
        return PyThreadObjectType.TryGetActiveThread(context.PyEnvironment)
            ?? PyThreadObjectType.CreateDummyThread(context.PyEnvironment);
    }

    [PyExport("currentThread", nameof(CurrentThreadAliasImpl))]
    public static partial PyBuiltinFunctionOrMethodObject CurrentThreadAlias { get; }

    [PyFunctionParameters()]
    private static PyResult CurrentThreadAliasImpl(PyCallContext context, PyArguments arguments)
    {
        var warnResult = context.Warn(
            PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedCurrentThread);
        if (warnResult.IsError)
            return warnResult;
        return CurrentThreadImpl(context, arguments);
    }

    [PyExport("active_count", nameof(ActiveCountImpl))]
    public static partial PyBuiltinFunctionOrMethodObject ActiveCount { get; }

    [PyFunctionParameters()]
    private static PyResult ActiveCountImpl(PyCallContext context, PyArguments arguments)
    {
        return PyIntObject.FromInteger(PyThreadObjectType.ActiveThreadCount(context.PyEnvironment));
    }

    [PyExport("activeCount", nameof(ActiveCountAliasImpl))]
    public static partial PyBuiltinFunctionOrMethodObject ActiveCountAlias { get; }

    [PyFunctionParameters()]
    private static PyResult ActiveCountAliasImpl(PyCallContext context, PyArguments arguments)
    {
        var warnResult = context.Warn(
            PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedActiveCount);
        if (warnResult.IsError)
            return warnResult;
        return ActiveCountImpl(context, arguments);
    }

    [PyExport("enumerate", nameof(EnumerateImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Enumerate { get; }

    [PyFunctionParameters()]
    private static PyResult EnumerateImpl(PyCallContext context, PyArguments arguments)
    {
        return PyListObject.CreateList(PyThreadObjectType.SnapshotActiveThreads(context.PyEnvironment));
    }

    [PyExport("main_thread", nameof(MainThreadImpl))]
    public static partial PyBuiltinFunctionOrMethodObject MainThread { get; }

    [PyFunctionParameters()]
    private static PyResult MainThreadImpl(PyCallContext context, PyArguments arguments)
    {
        return PyThreadObjectType.GetMainThread(context.PyEnvironment);
    }

    [PyExport("stack_size", nameof(StackSizeImpl))]
    public static partial PyBuiltinFunctionOrMethodObject StackSize { get; }

    [PyFunctionParameters("size=0")]
    private static PyResult StackSizeImpl(PyCallContext context, PyArguments arguments)
    {
        var converted = PySpecialMethods.Index(context, arguments[0]);
        if (converted.IsError)
            return converted;

        // "|n" parsing: an int outside the ssize_t range fails outright
        var requestedSize = converted.Value.Value;
        if (requestedSize > long.MaxValue || requestedSize < long.MinValue)
            return PyResult.RaiseException(PyOverflowErrorObjectType.Shared, PySR.Runtime_Number_Int_TooLargeForSsize);
        var size = (long)requestedSize;
        if (size is not 0 && size < PyThreadObjectType.MinimumStackSize)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Threading_StackSizeMin);

        // CPython quirk: the size argument defaults to 0, so even a bare
        // query resets the configured size, and every call returns the
        // value configured before the call
        return PyIntObject.FromInteger(PyThreadObjectType.ExchangeStackSize(context.PyEnvironment, size));
    }
}
