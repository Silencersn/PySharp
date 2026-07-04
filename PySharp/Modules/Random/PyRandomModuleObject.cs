using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Random;

[PyModuleInclude(PyModuleIncludeScheme.TypeSingleton, typeof(PyRandomObjectType))]
public partial class PyRandomModuleObject : PyModuleObject
{
    public PyRandomModuleObject() : base("random")
    {
    }

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        AppendAttribute("random", PyOperators.GetAttr(context, PyRandomObject.Shared, "random").Value!);
        AppendAttribute("uniform", PyOperators.GetAttr(context, PyRandomObject.Shared, "uniform").Value!);
        AppendAttribute("randrange", PyOperators.GetAttr(context, PyRandomObject.Shared, "randrange").Value!);
        AppendAttribute("randint", PyOperators.GetAttr(context, PyRandomObject.Shared, "randint").Value!);
    }
}
