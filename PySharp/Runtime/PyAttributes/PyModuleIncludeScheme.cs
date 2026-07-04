namespace PySharp.Runtime.PyAttributes;

public enum PyModuleIncludeScheme
{
    /// <summary>Scan all public static PyObject fields/properties of the specified type.</summary>
    StaticMembers,

    /// <summary>Register the .Shared singleton of the specified PyTypeObject type.</summary>
    TypeSingleton,

    /// <summary>Register an explicitly specified static member of a type with a given name.</summary>
    ExplicitMember,
}
