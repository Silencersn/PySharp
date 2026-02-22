using PySharp.Modules.Builtins;
using PySharp.Runtime;

namespace PySharp.Modules.CSharp;

internal sealed partial class UserDefinedType<TObject> : PyTypeObject<TObject> where TObject : PyObject
{
    public override string? DefaultModule => null;
    public override string Name { get; }
    public override IReadOnlyList<PyTypeObject> Bases { get; }
    internal override bool IsTypeImmutable => false;
    internal override bool IsImmutable => false;

    internal UserDefinedType(string name, string qualName, IReadOnlyList<PyTypeObject> bases) : base(name, bases, false)
    {
        Name = name;
        Bases = bases;
        PyAttributes.Add(PySpecialNames.QualName, PyStrObject.FromString(qualName));
    }
}
