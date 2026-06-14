using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;

namespace PySharp.Modules.Random;

public class PyRandomModuleObject : PyModuleObject
{
    public PyRandomModuleObject() : base("random")
    {
        AddObjToAttrs(PyRandomObjectType.Shared);
    }

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        AddObjToAttrs("random", PyOperators.GetAttr(context, PyRandomObject.Shared, "random").Value);
        AddObjToAttrs("uniform", PyOperators.GetAttr(context, PyRandomObject.Shared, "uniform").Value);
        AddObjToAttrs("randrange", PyOperators.GetAttr(context, PyRandomObject.Shared, "randrange").Value);
        AddObjToAttrs("randint", PyOperators.GetAttr(context, PyRandomObject.Shared, "randint").Value);
    }
}
