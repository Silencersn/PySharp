using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;

namespace PySharp.Modules.Site;

public class PySiteModuleObject : PyModuleObject
{
    public override string? Origin => "built-in";

    public PySiteModuleObject() : base("site")
    {
        //PyStandardLibrary.Builtins.AddObjToAttrs(PySiteFunctions.Exit);
        //PyStandardLibrary.Builtins.AddObjToAttrs(PySiteFunctions.Quit);
    }

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        var builtins = environment.LoadBuiltinModule(context, "builtins");
        builtins.AppendAttribute(PySiteFunctions.Exit);
        builtins.AppendAttribute(PySiteFunctions.Quit);
        builtins.AppendAttribute(PySiteFunctions.Help);
    }
}
