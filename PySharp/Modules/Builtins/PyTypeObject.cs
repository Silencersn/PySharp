using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

public abstract partial class PyTypeObject : PyObject, IPyObjectName
{
    public virtual IReadOnlyList<PyTypeObject> Bases => [PyObjectType.Shared];
    public IReadOnlyList<PyTypeObject> MRO { get; }
    public virtual string? DefaultModule => "builtins";
    public string? Module =>
        ModuleAsObject is PyStrObject str ? str.Value :
        ModuleAsObject is not null ? "<unknown>" : null;
    public PyObject? ModuleAsObject { get; internal set; }

    public abstract string Name { get; }
    public string FullName
    {
        get
        {
            var moduleName = Module;
            if (moduleName is null or PySpecialNames.Main or "builtins")
                return QualName;
            return $"{moduleName}.{QualName}";
        }
    }
    public virtual string QualName => Name;

    public virtual string Document => string.Empty;
    public virtual bool IsSealed => false;
    public override PyTypeObject DefaultPyType => PyTypeObjectType.Shared;
    public abstract Type LayoutType { get; }
    internal virtual bool IsTypeImmutable => true;

    internal PyTypeObject()
    {
        if (DefaultModule is not null)
            ModuleAsObject = PyStrObject.FromString(DefaultModule);
        MRO = [this, .. CreateMROWithoutSelf(Bases)];
        Slots = PyTypeSlots.Create(MRO.Skip(1));
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        PyAttributes.Add(PySpecialNames.Doc, PyNoneObject.None);
    }

    internal PyTypeObject(string name, IReadOnlyList<PyTypeObject> bases)
    {
        if (DefaultModule is not null)
            ModuleAsObject = PyStrObject.FromString(DefaultModule);
        MRO = [this, .. CreateMROWithoutSelf(bases)];
        Slots = PyTypeSlots.Create(MRO.Skip(1));
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(name));
        PyAttributes.Add(PySpecialNames.Doc, PyNoneObject.None);
    }

    public bool IsInstance(PyObject obj)
    {
        return obj.PyType.IsSubclassOf(this);
    }

    public bool IsSubclassOf(PyTypeObject pyType)
    {
        foreach (var baseType in MRO)
        {
            if (PyObjectComparer.Default.Equals(baseType, pyType))
                return true;
        }

        return false;
    }

    internal abstract PyTypeObject CreateUserDefinedTypeWithSameLayout(string name, string qualName, IReadOnlyList<PyTypeObject> bases);

    internal static void ValidateBases(PyCallContext context, IEnumerable<PyTypeObject> bases, out PyTypeObject layoutTypeOwner)
    {
        layoutTypeOwner = PyObjectType.Shared;
        foreach (var baseType in bases)
        {
            if (baseType.IsSealed)
                throw context.TypeError(PySR.Runtime_Inheritance_UnacceptableBaseType, baseType.Name);

            if (baseType.LayoutType == layoutTypeOwner.LayoutType)
                continue;

            if (baseType.LayoutType.IsSubclassOf(layoutTypeOwner.LayoutType))
                layoutTypeOwner = baseType;
            else if (!layoutTypeOwner.LayoutType.IsAssignableFrom(baseType.LayoutType))
                throw context.TypeError(PySR.Runtime_Inheritance_LayoutConflict);
        }

        if (!TryCreateMROWithoutSelf(bases, out _))
            throw context.TypeError(PySR.Runtime_Inheritance_CannotCreateMRO);
    }

    private static bool TryCreateMROWithoutSelf(IEnumerable<PyTypeObject> bases, [NotNullWhen(true)] out List<PyTypeObject>? mro)
    {
        // L[C(B1 ... BN)] = C + merge(L[B1] ... L[BN], B1 ... BN)
        mro = [];

        // B1 ... BN
        var baseTypes = new Queue<PyTypeObject>(bases);
        if (baseTypes.Count is 0)
        {
            // the type of object
            return true;
        }

        // L[B1] ... L[BN]
        List<Queue<PyTypeObject>> baseMros = [.. baseTypes.Select(baseType => new Queue<PyTypeObject>(baseType.MRO))];

        // L[B1] ... L[BN], B1 ... BN
        baseMros.Add(baseTypes);

        while (baseMros.Count > 0)
        {
            // take the head of the first list, i.e L[B1][0];
            // if this head is not in the tail of any of the other lists,
            // then add it to the linearization of C and remove it from the lists in the merge,
            // otherwise look at the head of the next list and take it, if it is a good head.
            //
            // Then repeat the operation until all the class are removed or it is impossible to find good heads.
            // In this case, it is impossible to construct the merge,
            // it will refuse to create the class C and will raise an exception.
            //
            for (int i = 0; i < baseMros.Count; i++)
            {
                var head = baseMros[i].Peek();
                bool notInOtherTails = true;
                for (int j = 0; j < baseMros.Count; j++)
                {
                    if (i == j)
                        continue;

                    var tail = baseMros[j].Skip(1);
                    if (tail.Contains(head))
                    {
                        notInOtherTails = false;
                        break;
                    }
                }
                if (notInOtherTails)
                {
                    mro.Add(head);
                    List<Queue<PyTypeObject>> baseMrosToRemove = [];
                    foreach (var baseMro in baseMros)
                    {
                        if (ReferenceEquals(baseMro.Peek(), head))
                        {
                            baseMro.Dequeue();
                            if (baseMro.Count is 0)
                                baseMrosToRemove.Add(baseMro);
                        }
                    }
                    foreach (var baseMroToRemove in baseMrosToRemove)
                    {
                        var removed = baseMros.Remove(baseMroToRemove);
                        Debug.Assert(removed);
                    }
                    break;
                }
                else if (i == baseMros.Count - 1)
                {
                    mro = null;
                    return false;
                }
            }
        }

        return true;
    }


    private static List<PyTypeObject> CreateMROWithoutSelf(IEnumerable<PyTypeObject> bases)
    {
        if (TryCreateMROWithoutSelf(bases, out var mro))
            return mro;

        throw new UnreachableException();
    }
}