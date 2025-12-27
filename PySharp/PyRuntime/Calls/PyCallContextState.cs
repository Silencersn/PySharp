using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime.Calls;

internal sealed class PyCallContextState
{
    private PyFrame _currentFrame;

    internal PyCallContextState(PyFrame rootFrame)
    {
        _currentFrame = rootFrame;
    }

    public PyFrame CurrentFrame
    {
        get => _currentFrame;
        set => _currentFrame = value;
    }
}
