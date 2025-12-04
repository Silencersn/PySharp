using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace PySharp.PyModules.Builtins;

public class PySliceObject : PyObject
{
    public PyObject Start { get; }
    public PyObject Stop { get; }
    public PyObject Step { get; }

    public PySliceObject(PyObject start, PyObject stop, PyObject step)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(step);

        Start = start;
        Stop = stop;
        Step = step;
    }
}