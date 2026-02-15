using PySharp.Modules.Builtins;

namespace PySharp.Modules.Time;

public class PyTimeModuleObject : PyModuleObject
{
    public PyTimeModuleObject() : base("time")
    {
        AddObjToAttrs(PyTimeFunctions.Time); // time
    }
}
