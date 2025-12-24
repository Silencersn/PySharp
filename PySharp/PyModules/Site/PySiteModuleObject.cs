using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Environments;

namespace PySharp.PyModules.Site;

public class PySiteModuleObject : PyModuleObject
{
    public override string? ReprPrompt => "(frozen)";

    public PySiteModuleObject() : base("site")
    {
        //PyStandardLibrary.Builtins.AddObjToAttrs(PySiteFunctions.Exit);
        //PyStandardLibrary.Builtins.AddObjToAttrs(PySiteFunctions.Quit);
    }

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        var builtins = environment.LoadBuiltinModule(context, "builtins");
        builtins.AddObjToAttrs(PySiteFunctions.Exit);
        builtins.AddObjToAttrs(PySiteFunctions.Quit);
    }
}
