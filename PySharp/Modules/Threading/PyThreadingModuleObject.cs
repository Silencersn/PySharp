using PySharp.Modules.Builtins;

namespace PySharp.Modules.Threading;

public class PyThreadingModuleObject : PyModuleObject
{
    public PyThreadingModuleObject() : base("threading")
    {
        AddObjToAttrs(PyThreadObjectType.Shared);
    }
}
