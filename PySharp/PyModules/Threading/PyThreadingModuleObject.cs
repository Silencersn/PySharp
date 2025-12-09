using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyModules.Threading;

public class PyThreadingModuleObject : PyModuleObject
{
    public PyThreadingModuleObject() : base("threading")
    {
        AddObjToAttrs(PyThreadObjectType.Shared);
    }
}
