using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime.Calls;

internal sealed class PyCallContextState
{
    private PyFrame _currentFrame;
    private PyExceptionObject? _currentException;

    internal PyCallContextState(PyFrame rootFrame)
    {
        _currentFrame = rootFrame;
    }

    public PyFrame CurrentFrame
    {
        get => _currentFrame;
        set => _currentFrame = value;
    }
    public PyExceptionObject? CurrentException
    {
        get => _currentException;
        set => _currentException = value;
    }
}
