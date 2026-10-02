using PySharp.Modules.Builtins;

namespace PySharp.Modules.CSharp;

internal sealed partial class UserDefinedType<TObject> : PyTypeObject<TObject> where TObject : PyObject
{
    protected override string? DefaultModule => null;
    protected override string DefaultName { get; }
    private IReadOnlyList<PyTypeObject> _bases;
    public override IReadOnlyList<PyTypeObject> Bases => _bases;
    internal override bool InstancesAreImmutable => false;
    internal override bool IsImmutable => false;
    internal override bool IsRuntimeCreated => true;

    // the only shape __bases__ assignment can reach: the base class's
    // Bases is the immutable [object] sentinel
    internal override void OverwriteBases(IReadOnlyList<PyTypeObject> bases) => _bases = bases;

    internal UserDefinedType(string name, string qualName, IReadOnlyList<PyTypeObject> bases) : base(qualName, bases, false)
    {
        // the base ctor derives Name from the qualname's last segment;
        // an explicit __qualname__ whose tail differs from the name
        // (class-body override, type(..., {'__qualname__': ...})) would
        // otherwise clobber the real name
        Name = name;
        DefaultName = name;
        _bases = bases;
    }
}
