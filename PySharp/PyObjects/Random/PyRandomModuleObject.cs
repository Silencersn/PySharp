using PySharp.PyObjects.Builtins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PySharp.PyObjects.Random;

public class PyRandomModuleObject : PyModuleObject
{
    public PyRandomModuleObject() : base("random")
    {
        AddObjToAttrs("random", PyRandomObject.Shared.GetAttribute("random"));
        AddObjToAttrs("uniform", PyRandomObject.Shared.GetAttribute("uniform"));
        AddObjToAttrs("randrange", PyRandomObject.Shared.GetAttribute("randrange"));
        AddObjToAttrs("randint", PyRandomObject.Shared.GetAttribute("randint"));
        AddObjToAttrs(PyRandomObjectType.Shared);
    }
}
