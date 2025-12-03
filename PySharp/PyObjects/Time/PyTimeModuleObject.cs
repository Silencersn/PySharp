using PySharp.PyObjects.Builtins;

namespace PySharp.PyObjects.Time;

public class PyTimeModuleObject : PyModuleObject
{
    public PyTimeModuleObject() : base("time")
    {
        AddObjToAttrs(PyTimeFunctions.Time); // time
    }
}
