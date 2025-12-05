using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Environments;

namespace PySharp.PyModules.Site;

public class PySiteModuleObject : PyModuleObject
{
    public PySiteModuleObject() : base("site")
    {
        //PyStandardLibrary.Builtins.AddObjToAttrs(PySiteFunctions.Exit);
        //PyStandardLibrary.Builtins.AddObjToAttrs(PySiteFunctions.Quit);
    }

    public override void OnImport(PyEnvironment environment)
    {
        var builtins = environment.LoadBuiltinModule("builtins");
        builtins.AddObjToAttrs(PySiteFunctions.Exit);
        builtins.AddObjToAttrs(PySiteFunctions.Quit);
    }
}
