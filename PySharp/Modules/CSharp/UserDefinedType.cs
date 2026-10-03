using PySharp.Modules.Builtins;

namespace PySharp.Modules.CSharp;

internal sealed partial class UserDefinedType<TObject> : PyTypeObject<TObject> where TObject : PyObject
{
    private readonly bool _excludesInstanceDict;

    protected override string? DefaultModule => null;
    protected override string DefaultName { get; }
    private IReadOnlyList<PyTypeObject> _bases;
    public override IReadOnlyList<PyTypeObject> Bases => _bases;
    internal override bool InstancesAreImmutable => false;

    // the two immutability faces come apart for __slots__: a class whose
    // slots exclude the instance dict keeps writable type attributes (a heap
    // type), while its instances are the dict-less shape — undeclared names
    // hit the "no __dict__" error and __dict__ reads miss
    internal override bool IsImmutable => _excludesInstanceDict;
    internal override bool InstancesCarryInstanceDict => !_excludesInstanceDict;
    internal override bool IsRuntimeCreated => true;

    // the only shape __bases__ assignment can reach: the base class's
    // Bases is the immutable [object] sentinel
    internal override void OverwriteBases(IReadOnlyList<PyTypeObject> bases) => _bases = bases;

    internal UserDefinedType(string name, string qualName, IReadOnlyList<PyTypeObject> bases, bool excludesInstanceDict = false) : base(qualName, bases, false)
    {
        // the base ctor derives Name from the qualname's last segment;
        // an explicit __qualname__ whose tail differs from the name
        // (class-body override, type(..., {'__qualname__': ...})) would
        // otherwise clobber the real name
        Name = name;
        DefaultName = name;
        _bases = bases;
        _excludesInstanceDict = excludesInstanceDict;
    }
}
