using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyModules.Queue;

public sealed class PyQueueModuleObject : PyModuleObject
{
    public PyQueueModuleObject() : base("queue")
    {
        AddObjToAttrs(PyQueueObjectType.Shared);
        AddObjToAttrs(PyFullObjectType.Shared);
        AddObjToAttrs(PyEmptyObjectType.Shared);
        AddObjToAttrs(PyShutDownObjectType.Shared);
    }
}
