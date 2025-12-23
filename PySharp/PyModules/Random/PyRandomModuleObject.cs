using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Random;

public class PyRandomModuleObject : PyModuleObject
{
    public PyRandomModuleObject() : base("random")
    {
        AddObjToAttrs("random", PyRandomObject.Shared.GetAttribute(PyCallContext.NonContextDependency, "random").Value);
        AddObjToAttrs("uniform", PyRandomObject.Shared.GetAttribute(PyCallContext.NonContextDependency, "uniform").Value);
        AddObjToAttrs("randrange", PyRandomObject.Shared.GetAttribute(PyCallContext.NonContextDependency, "randrange").Value);
        AddObjToAttrs("randint", PyRandomObject.Shared.GetAttribute(PyCallContext.NonContextDependency, "randint").Value);
        AddObjToAttrs(PyRandomObjectType.Shared);
    }
}
