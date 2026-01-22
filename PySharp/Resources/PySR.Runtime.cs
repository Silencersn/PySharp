namespace PySharp.Resources;

partial class PySR
{
    public const string Runtime_Keyword_KeywordsMustBeStrings = "keywords must be strings";

    public const string Runtime_TryStmt_CatchNonException = "catching classes that do not inherit from BaseException is not allowed";
    public const string Runtime_TryStmt_SplitReturnsNonTuple = "{0}.split must return a tuple, not {1}";
    public const string Runtime_TryStmt_SplitReturnsTupleWithWrongSize = "{0}.split must return a 2-tuple, got tuple of size {1}";
    public const string Runtime_TryStmt_ExpectedExceptionOrNone = "Exception expected for value, {0} found";

    public const string Runtime_MatchStmt_CallNonClass = "called match pattern must be a class";
    public const string Runtime_MatchStmt_MatchArgsIsNonTuple = "{0}.__match_args__ must be a tuple (got {1})";
    public const string Runtime_MatchStmt_MatchArgsLengthNotEnough = "{0}() accepts {1} positional sub-patterns ({2} given)";
    public const string Runtime_MatchStmt_MatchArgsEltMustBeString = "__match_args__ elements must be strings (got {0})";

    public const string Runtime_WithStmt_MissingEnter = "'{0}' object does not support the context manager protocol (missed __enter__ method)";
    public const string Runtime_WithStmt_MissingExit = "'{0}' object does not support the context manager protocol (missed __exit__ method)";

    public const string Runtime_RaiseStmt_RaiseNonException = "exceptions must derive from BaseException";
    public const string Runtime_RaiseStmt_Cause = "The above exception was the direct cause of the following exception:";

    public const string Runtime_Import_ModuleNotFound = "No module named '{0}'";
    public const string Runtime_Import_NonIterableAll = "{0}.__all__ must be iterable";
    public const string Runtime_Import_NonStringAllElt = "Item in {0}.__all__ must be str, not {1}";
    public const string Runtime_Import_CannotImportName = "cannot import name '{0}' from '{1}'";

    public const string Runtime_Inheritance_UnacceptableBaseType = "type '{0}' is not an acceptable base type";
    public const string Runtime_Inheritance_LayoutConflict = "multiple bases have instance lay-out conflict";
    public const string Runtime_Inheritance_CannotCreateMRO = "Cannot create a consistent method resolution order (MRO)";

    public const string Runtime_Assignment_UnpackCountNotMatch = "too many or too few values to unpack";

    public const string Runtime_Type_MethodReceiveSelfWithWrongType = "'{0}' requires a '{1}' object but received a '{2}'";
    public const string Runtime_Type_AttributeNotFound = "type object '{0}' has no attribute '{1}'";

    public const string Runtime_Object_SpecialMethodReturnsWrongType = "{0} returned non-{1} (type {2})";
    public const string Runtime_Object_Unhashable = "unhashable type: '{0}'";
    public const string Runtime_Object_NonCallable = "'{0}' object is not callable";
    public const string Runtime_Object_FormatReturnsNonString = "__format__ must return a str, not {0}";
    public const string Runtime_Object_FormatArgumentNonString = "format() argument 2 must be str, not {0}";
    public const string Runtime_Object_FormatUnsupported = "unsupported format string passed to {0}.__format__";
    public const string Runtime_Object_AttributeMustBeString = "attribute name must be string, not '{0}'";
    public const string Runtime_Object_AttributeNotFound = "'{0}' object has no attribute '{1}'";

    public const string Runtime_Number_CannotInterpretedAsInteger = "'{0}' object cannot be interpreted as an integer";
    public const string Runtime_Number_WrongFloatArg = "float() argument must be a string or a real number, not '{0}'";

    public const string Runtime_Sequence_NegativeLen = "__len__() should return >= 0";
    public const string Runtime_Sequence_NoLen = "object of type '{0}' has no len()";
    public const string Runtime_Sequence_NonIterable = "'{0}' object is not iterable";
    public const string Runtime_Sequence_IterReturnsNonIterator = "iter() returned non-iterator of type '{0}'";
    public const string Runtime_Sequence_NonSubscriptable = "'{0}' object is not subscriptable";

    public const string Runtime_Operator_UnsupportedForDivmod = "unsupported operand type(s) for divmod(): '{0}' and '{1}'";
    public const string Runtime_Operator_UnsupportedForAbs = "bad operand type for abs(): '{0}'";

}
