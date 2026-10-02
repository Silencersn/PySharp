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

    // which CPython mechanism the property models: true builds a
    // getset_descriptor face (tp_getset, e.g. float.real or type.__mro__),
    // the default builds member_descriptor for the READONLY PyMemberDef
    // tables most generated members come from; the face decides the repr
    // and the message of a failed write
    public bool GetSet { get; set; }
}

public enum PyPropertyMethodType
{
    Getter = 0,
    Setter = 1,
    Deleter = 2,
}
