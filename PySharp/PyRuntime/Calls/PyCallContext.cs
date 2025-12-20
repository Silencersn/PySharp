using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime.Calls;

public class PyCallContext
{
    internal static PyCallContext Null { get; } = new();
    internal PyCallContext()
    {
    }
}
