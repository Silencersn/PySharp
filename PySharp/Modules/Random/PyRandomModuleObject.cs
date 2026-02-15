using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Random;

public class PyRandomModuleObject : PyModuleObject
{
    public PyRandomModuleObject() : base("random")
    {
        AddObjToAttrs("random", PyOperators.GetAttr(PyCallContext.NonContextDependency, PyRandomObject.Shared, "random").Value);
        AddObjToAttrs("uniform", PyOperators.GetAttr(PyCallContext.NonContextDependency, PyRandomObject.Shared, "uniform").Value);
        AddObjToAttrs("randrange", PyOperators.GetAttr(PyCallContext.NonContextDependency, PyRandomObject.Shared, "randrange").Value);
        AddObjToAttrs("randint", PyOperators.GetAttr(PyCallContext.NonContextDependency, PyRandomObject.Shared, "randint").Value);
        AddObjToAttrs(PyRandomObjectType.Shared);
    }
}
