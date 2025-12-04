using PySharp.PyModules.Builtins;

namespace PySharp.PyModules.Time;

public class PyTimeModuleObject : PyModuleObject
{
    public PyTimeModuleObject() : base("time")
    {
        AddObjToAttrs(PyTimeFunctions.Time); // time
    }
}
