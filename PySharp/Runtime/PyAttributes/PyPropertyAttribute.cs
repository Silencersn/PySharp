namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PyPropertyAttribute : PyAttribute
{
    public PyPropertyAttribute(string name)
    {
        Name = name;
        Type = PyPropertyMethodType.Getter;
    }

    public string Name { get; }
    public PyPropertyMethodType Type { get; set; }

    // which CPython mechanism the property models, which decides the
    // message of a failed write: a tp_getset without a setter reports
    // "attribute 'x' of 'T' objects is not writable" (descrobject.c
    // gset_set), while a READONLY PyMemberDef keeps the bare
    // "readonly attribute" of PyMember_SetOne (structmember.c) — the
    // default, since most generated members model PyMemberDef tables
    public bool GetSet { get; set; }
}

public enum PyPropertyMethodType
{
    Getter = 0,
    Setter = 1,
    Deleter = 2,
}
