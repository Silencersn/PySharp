using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime;


internal readonly struct YieldCallerAction
{
    public readonly ActionType Type;
    public readonly PyObject? Value;

    public YieldCallerAction(ActionType type, PyObject? value)
    {
        Type = type;
        Value = value;
    }

    public static YieldCallerAction Next()
    {
        return new YieldCallerAction(ActionType.Next, null);
    }
    public static YieldCallerAction Send(PyObject value)
    {
        return new YieldCallerAction(ActionType.Send, value);
    }
    public static YieldCallerAction Throw(PyObject value)
    {
        return new YieldCallerAction(ActionType.Throw, value);
    }
    public static YieldCallerAction Close()
    {
        return new YieldCallerAction(ActionType.Close, null);
    }

    public enum ActionType
    {
        None = 0,
        Next,
        Send,
        Throw,
        Close
    }
}


partial class PyFrame
{
    internal bool _generatorCompleted;
    internal TaskCompletionSource<YieldCallerAction>? _tcsWaitAtStartOrYield;
    internal TaskCompletionSource<PyResult>? _tcsWaitAtSend;
}
