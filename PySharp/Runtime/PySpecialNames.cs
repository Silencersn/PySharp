namespace PySharp.Runtime;

public static partial class PySpecialNames
{
    // methods
    public const string New = "__new__";
    public const string InitSubclass = "__init_subclass__";

    // attributes
    public const string Bases = "__bases__";
    public const string Name = "__name__";
    public const string Value = "__value__";
    public const string Self = "__self__";
    public const string Func = "__func__";
    public const string Doc = "__doc__";
    public const string Code = "__code__";

    public const string Builtins = "__builtins__";
    public const string Main = "__main__";
    public const string Debug = "__debug__";
    public const string All = "__all__";
    public const string Class = "__class__";
    public const string MRO = "__mro__";
    public const string Closure = "__closure__";
    public const string Globals = "__globals__";
    public const string Module = "__module__";
    public const string QualName = "__qualname__";

    public const string Path = "__path__";

    public const string MatchArgs = "__match_args__";

    // exception attributes
    public const string Cause = "__cause__";
    public const string Context = "__context__";
    public const string Traceback = "__traceback__";
    public const string SuppressContext = "__suppress_context__";


    // functions
    public const string Import = "__import__";

    internal static partial IEnumerable<string> EnumerateNonGeneratedNames();
    internal static partial IEnumerable<string> EnumerateGeneratedNames();

    public static partial class Interned;
}
