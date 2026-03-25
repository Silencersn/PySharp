using PySharp.Modules.Builtins;

namespace PySharp.Modules.CSharp;

internal sealed partial class UserDefinedType<TObject> : PyTypeObject<TObject> where TObject : PyObject
{
    protected override string? DefaultModule => null;
    protected override string DefaultName { get; }
    public override IReadOnlyList<PyTypeObject> Bases { get; }
    internal override bool IsTypeImmutable => false;
    internal override bool IsImmutable => false;

    internal UserDefinedType(string name, string qualName, IReadOnlyList<PyTypeObject> bases) : base(qualName, bases, false)
    {
        DefaultName = name;
        Bases = bases;
    }
}
