using PySharp.PyModules.Builtins;

namespace PySharp.PyModules.Threading;

public class PyThreadingModuleObject : PyModuleObject
{
    public PyThreadingModuleObject() : base("threading")
    {
        AddObjToAttrs(PyThreadObjectType.Shared);
    }
}
