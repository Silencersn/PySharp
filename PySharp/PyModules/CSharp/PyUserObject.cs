using PySharp.PyModules.Builtins;

namespace PySharp.PyModules.CSharp;

public abstract class PyUserObject<TSuper> : PyObject where TSuper : PyObject
{
    public TSuper Super { get; }

    public PyUserObject(TSuper super)
    {
        ArgumentNullException.ThrowIfNull(super);

        Super = super;
    }
}

public class PyUserObject : PyUserObject<PyObject>
{
    public PyUserObject() : base(new PyObject())
    {
    }
}