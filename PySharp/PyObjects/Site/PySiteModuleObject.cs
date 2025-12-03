using PySharp.PyObjects.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Environments;
using System.Diagnostics;

namespace PySharp.PyObjects.Site;

public class PySiteModuleObject : PyModuleObject
{
    public PySiteModuleObject() : base("site")
    {
        //PyStandardLibrary.Builtins.AddObjToAttrs(PySiteFunctions.Exit);
        //PyStandardLibrary.Builtins.AddObjToAttrs(PySiteFunctions.Quit);
    }

    public override void OnImport(PyEnvironment environment)
    {
        var builtins = environment.ImportModule("builtins");
        Debug.Assert(builtins is not null);
        builtins.AddObjToAttrs(PySiteFunctions.Exit);
        builtins.AddObjToAttrs(PySiteFunctions.Quit);
    }
}
