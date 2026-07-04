namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PyModuleIncludeAttribute : PyAttribute
{
    /// <summary>The registration scheme to use.</summary>
    public PyModuleIncludeScheme Scheme { get; }

    /// <summary>The source type to scan for PyObject members.</summary>
    public Type SourceType { get; }

    /// <summary>Explicit Python attribute name (for <see cref="PyModuleIncludeScheme.ExplicitMember"/>).</summary>
    public string? Name { get; }

    /// <summary>Member name within <see cref="SourceType"/> (for <see cref="PyModuleIncludeScheme.ExplicitMember"/>).</summary>
    public string? MemberName { get; }

    /// <summary>
    /// For <see cref="PyModuleIncludeScheme.StaticMembers"/> or <see cref="PyModuleIncludeScheme.TypeSingleton"/>:
    /// scan the type's static members or register its .Shared singleton.
    /// </summary>
    public PyModuleIncludeAttribute(PyModuleIncludeScheme scheme, Type sourceType)
    {
        Scheme = scheme;
        SourceType = sourceType;
    }

    /// <summary>
    /// For <see cref="PyModuleIncludeScheme.ExplicitMember"/>:
    /// register a specific static member of a type with the given Python name.
    /// </summary>
    public PyModuleIncludeAttribute(PyModuleIncludeScheme scheme, string name, Type sourceType, string memberName)
    {
        Scheme = scheme;
        Name = name;
        SourceType = sourceType;
        MemberName = memberName;
    }
}
