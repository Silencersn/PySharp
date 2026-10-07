namespace PySharp.Runtime;

public static partial class PySpecialNames
{
    // methods
    public const string New = "__new__";
    public const string InitSubclass = "__init_subclass__";

    // comparison dunders: the six names resolve onto the single
    // RichCompare slot, so they left the generator's declaration manifest
    // and live here as plain constants
    public const string Lt = "__lt__";
    public const string Le = "__le__";
    public const string Eq = "__eq__";
    public const string Ne = "__ne__";
    public const string Gt = "__gt__";
    public const string Ge = "__ge__";

    // reflected dunders: the names resolve onto the forward slots (the
    // reflection protocol lives inside the slot implementations, like
    // CPython's SLOT1BINFULL slot_nb_add), so they too left the
    // declaration manifest and live here as plain constants
    public const string RAdd = "__radd__";
    public const string RSub = "__rsub__";
    public const string RMul = "__rmul__";
    public const string RMatMul = "__rmatmul__";
    public const string RTrueDiv = "__rtruediv__";
    public const string RFloorDiv = "__rfloordiv__";
    public const string RMod = "__rmod__";
    public const string RDivMod = "__rdivmod__";
    public const string RPow = "__rpow__";
    public const string RLShift = "__rlshift__";
    public const string RRShift = "__rrshift__";
    public const string RAnd = "__rand__";
    public const string RXor = "__rxor__";
    public const string ROr = "__ror__";

    // attributes
    public const string Bases = "__bases__";
    public const string Name = "__name__";
    public const string Value = "__value__";
    public const string Self = "__self__";
    public const string SelfClass = "__self_class__";
    public const string ThisClass = "__thisclass__";
    public const string Func = "__func__";
    public const string Doc = "__doc__";
    public const string Code = "__code__";

    public const string Builtins = "__builtins__";
    public const string Main = "__main__";
    public const string Debug = "__debug__";
    public const string All = "__all__";
    public const string Class = "__class__";
    public const string MRO = "__mro__";
    public const string Slots = "__slots__";
    public const string Weakref = "__weakref__";
    public const string Closure = "__closure__";
    public const string Globals = "__globals__";
    public const string Module = "__module__";
    public const string QualName = "__qualname__";

    public const string Path = "__path__";
    public const string Package = "__package__";
    public const string File = "__file__";

    public const string Dir = "__dir__";
    public const string FirstLineNo = "__firstlineno__";

    // PEP 3115 metaclass hook
    public const string Prepare = "__prepare__";

    // type-check hooks, resolved on the type of the second argument
    public const string InstanceCheck = "__instancecheck__";
    public const string SubclassCheck = "__subclasscheck__";

    public const string MatchArgs = "__match_args__";
    public const string LengthHint = "__length_hint__";

    public const string Dict = "__dict__";
    public const string Defaults = "__defaults__";
    public const string Notes = "__notes__";

    // exception attributes
    public const string Cause = "__cause__";
    public const string Context = "__context__";
    public const string Traceback = "__traceback__";
    public const string SuppressContext = "__suppress_context__";


    // generic/typing
    public const string ClassGetItem = "__class_getitem__";
    public const string Origin = "__origin__";
    public const string Args = "__args__";
    public const string Parameters = "__parameters__";
    public const string TypeParams = "__type_params__";

    // annotations
    public const string Annotations = "__annotations__";
    public const string Annotate = "__annotate__";
    public const string AnnotateFunc = "__annotate_func__";
    public const string ConditionalAnnotations = "__conditional_annotations__";
    public const string AnnotationsCache = "__annotations_cache__";

    // functions
    public const string Import = "__import__";

    // sys.displayhook REPL binding
    public const string Underscore = "_";

    internal static partial IEnumerable<string> EnumerateNonGeneratedNames();
    internal static partial IEnumerable<string> EnumerateGeneratedNames();

    public static partial class Interned;
}
