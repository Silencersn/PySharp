using PySharp.PyRuntime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace PySharp.PyModules.Builtins;

public class PyStrIteratorObject : PyObject
{
    private StringRuneEnumerator _enumerator;

    internal PyStrIteratorObject(string str)
    {
        _enumerator = str.EnumerateRunes();
    }

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        if (!_enumerator.MoveNext())
            return PyVirtualMachine.RaiseStopIteration();

        return PyStrObject.FromRune(_enumerator.Current);
    }
}
