using System.Collections.Immutable;

namespace PySharp.PyRuntime;

public static class PySpecialNames
{
    public const string Init = "__init__";
    public const string New = "__new__";

    public const string Name = "__name__";
    public const string Self = "__self__";
    public const string Bases = "__bases__";

    public const string Repr = "__repr__";
    public const string Len = "__len__";
    public const string Hash = "__hash__";
    public const string Iter = "__iter__";
    public const string Next = "__next__";
    public const string Abs = "__abs__";

    public const string Bool = "__bool__";
    public const string Str = "__str__";
    public const string Int = "__int__";
    public const string Float = "__float__";
    public const string Index = "__index__";

    public const string Builtins = "__builtins__";
    public const string Main = "__main__";
    public const string Debug = "__debug__";
}
