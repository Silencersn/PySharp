using PySharp.PyModules.Builtins;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime;

internal static class PySpecialCaller
{
    public static PyObject? Repr(PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Repr();

        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Repr);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }

    public static PyObject? Str(PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Str();

        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Str);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
}
