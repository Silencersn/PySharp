using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime;

partial class PyFrame
{
    internal TaskCompletionSource<PyObject>? _tcsWaitAtStartOrYield;
    internal TaskCompletionSource<PyResult>? _tcsWaitAtSend;
}
