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
    public const string Runtime_Assignment_NotEnoughToUnpack = "not enough values to unpack (expected {0}, got {1})";
    public const string Runtime_Assignment_NotEnoughToUnpackStarred = "not enough values to unpack (expected at least {0}, got {1})";
    public const string Runtime_Assignment_TooManyToUnpack = "too many values to unpack (expected {0}, got {1})";

    public const string Runtime_Type_MethodReceiveSelfWithWrongType = "'{0}' requires a '{1}' object but received a '{2}'";
    public const string Runtime_Type_AttributeNotFound = "type object '{0}' has no attribute '{1}'";
    public const string Runtime_Type_CannotCreateInstance = "cannot create '{0}' instances";
    public const string Runtime_Type_NewClsNonType = "{0}.__new__(X): X is not a type object ({1})";
    public const string Runtime_Type_NewClsNotSubtype = "{0}.__new__({1}): {1} is not a subtype of {0}";
    public const string Runtime_Type_NewClsNotSafe = "{0}.__new__({1}) is not safe, use {1}.__new__()";
    public const string Runtime_Type_SetImmutable = "cannot set '{0}' attribute of immutable type '{1}'";

    public const string Runtime_Object_SpecialMethodReturnsWrongType = "{0} returned non-{1} (type {2})";
    public const string Runtime_Object_Unhashable = "unhashable type: '{0}'";
    public const string Runtime_Object_NonCallable = "'{0}' object is not callable";
    public const string Runtime_Object_FormatReturnsNonString = "__format__ must return a str, not {0}";
    public const string Runtime_Object_FormatArg2NonString = "format() argument 2 must be str, not {0}";
    public const string Runtime_Object_FormatUnsupported = "unsupported format string passed to {0}.__format__";
    public const string Runtime_Object_FormatSpecInvalid = "Invalid format specifier '{0}' for object of type '{1}'";
    public const string Runtime_Object_AttributeMustBeString = "attribute name must be string, not '{0}'";
    public const string Runtime_Object_AttributeNotFound = "'{0}' object has no attribute '{1}'";
    public const string Runtime_Object_NewTakesExactlyOneArg = "object.__new__() takes exactly one argument (the type to instantiate)";

    public const string Runtime_String_IndexOutOfRange = "string index out of range";
    public const string Runtime_String_AddNonStr = "can only concatenate str (not \"{0}\") to str";
    public const string Runtime_String_JoinNonStrAt = "sequence item {0}: expected str instance, {1} found";

    public const string Runtime_Super_ObjNotMatchType = "super(type, obj): obj must be an instance or subtype of type";
    public const string Runtime_Super_NoArgs = "super(): no arguments";
    public const string Runtime_Super_ClassCellNotFound = "super(): __class__ cell not found";
    public const string Runtime_Super_ClassCellEmpty = "super(): empty __class__ cell";
    public const string Runtime_Super_ClassNonType = "super(): __class__ is not a type ({0})";
    public const string Runtime_Super_Arg1MustBeType = "super() argument 1 must be a type, not {0}";

    public const string Runtime_Number_Int_CannotInterpretedAsInt = "'{0}' object cannot be interpreted as an integer";
    public const string Runtime_Number_Int_WrongArg = "int() argument must be a string, a bytes-like object or a real number, not '{0}'";
    public const string Runtime_Number_Int_BaseOutOfRange = "int() base must be >= 2 and <= 36, or 0";
    public const string Runtime_Number_Int_ConvertNonStr = "int() can't convert non-string with explicit base";
    public const string Runtime_Number_Int_InvalidLiteral = "invalid literal for int() with base {0}: '{1}'";
    public const string Runtime_Number_Float_WrongArg = "float() argument must be a string or a real number, not '{0}'";
    public const string Runtime_Number_PowWithZeroModulo = "pow() 3rd argument cannot be 0";
    public const string Runtime_Number_DivisionByZero = "division by zero";

    public const string Runtime_Sequence_NegativeLen = "__len__() should return >= 0";
    public const string Runtime_Sequence_NoLen = "object of type '{0}' has no len()";
    public const string Runtime_Sequence_NonIterable = "'{0}' object is not iterable";
    public const string Runtime_Sequence_IterReturnsNonIterator = "iter() returned non-iterator of type '{0}'";
    public const string Runtime_Sequence_NonSubscriptable = "'{0}' object is not subscriptable";

    public const string Runtime_List_ItemNotFound = "list.{0}(x): x not in list";
    public const string Runtime_List_PopIndexOutOfRange = "pop index out of range";
    public const string Runtime_List_IndexOutOfRange = "list index out of range";

    public const string Runtime_Dictionary_UpdateEltLengthNotMatch = "dictionary update sequence element #{0} has length {1}; 2 is required";
    public const string Runtime_Dictionary_PopEmptyDict = "popitem(): dictionary is empty";
    public const string Runtime_Dictionary_NotAMapping = "'{0}' object is not a mapping";

    public const string Runtime_Range_Arg3Zero = "range() arg 3 must not be zero";

    public const string Runtime_Zip_SecondShorterThanFirst = "zip() argument 2 is shorter than argument 1";
    public const string Runtime_Zip_NthShorterThanPrevious = "zip() argument {0} is shorter than arguments 1-{1}";
    public const string Runtime_Zip_SecondLongerThanFirst = "zip() argument 2 is longer than argument 1";
    public const string Runtime_Zip_NthLongerThanPrevious = "zip() argument {0} is longer than arguments 1-{1}";

    public const string Runtime_Operator_UnsupportedForDivmod = "unsupported operand type(s) for divmod(): '{0}' and '{1}'";
    public const string Runtime_Operator_UnsupportedForAbs = "bad operand type for abs(): '{0}'";
    public const string Runtime_Operator_UnsupportedForUnary = "bad operand type for unary {0}: '{1}'";
    public const string Runtime_Operator_UnsupportedBetween = "'{0}' not supported between instances of '{1}' and '{2}'";

    public const string Runtime_Builtin_Print_WrongArgType = "{0} must be None or a string, not {1}";
    public const string Runtime_Builtin_Max_EmptyIterable = "max() iterable argument is empty";
    public const string Runtime_Builtin_Min_EmptyIterable = "min() iterable argument is empty";
    public const string Runtime_Builtin_Sum_Strings = "sum() can't sum strings [use ''.join(seq) instead]";
    public const string Runtime_Builtin_Chr_OutOfRange = "chr() arg not in range(0x110000)";
    public const string Runtime_Builtin_Ord_ExpectedString = "ord() expected string of length 1, but {0} found";
    public const string Runtime_Builtin_Ord_ExpectedACharacter = "ord() expected a character, but string of length {0} found";
    public const string Runtime_Builtin_Import_NameMustBeString = "module name must be a string";
    public const string Runtime_Builtin_IsInstance_MustBeTypeOrTupleOfTypes = "isinstance() arg 2 must be a type or a tuple of types";
    public const string Runtime_Builtin_IsSubclass_Arg1MustBeClass = "issubclass() arg 1 must be a class";
    public const string Runtime_Builtin_IsSubclass_Arg2MustBeTypeOrTupleOfTypes = "issubclass() arg 2 must be a type or a tuple of types";
    public const string Runtime_Builtin_ExecEval_Globals = "globals must be a dict";
    public const string Runtime_Builtin_ExecEval_Locals = "locals must be a dict";
    public const string Runtime_Builtin_Exec_ClosureForNonCodeObj = "closure can only be used when source is a code object";
    public const string Runtime_Builtin_Exec_WrongClosure = "code object requires a closure of exactly length {0}";
    public const string Runtime_Builtin_ExecEval_Arg1WrongType = "{0}() arg 1 must be a string, bytes or code object";
    public const string Runtime_Builtin_Exec_CannotUseClosure = "cannot use a closure with this code object";
    public const string Runtime_Builtin_Eval_PassCodeObjWithFreeVars = "code object passed to eval() may not contain free variables";
    public const string Runtime_Builtin_Compile_Arg1WrongType = "compile() arg 1 must be a string, bytes or AST object";
    public const string Runtime_Builtin_Compile_WrongMode = "compile() mode must be 'exec', 'eval' or 'single'";
    public const string Runtime_Builtin_Compile_FilenameWrongType = "expected str, bytes or os.PathLike object, not {0}";
    public const string Runtime_Builtin_Compile_ModeWrongType = "compile() argument 'mode' must be str, not {0}";
    public const string Runtime_Builtin_Reversed_NonReversible = "'{0}' object is not reversible";

    public const string Runtime_Descriptor_GetNoneNoneInvalid = "__get__(None, None) is invalid";
    public const string Runtime_Descriptor_ReceiveObjectOfWrongType = "descriptor '{0}' requires a '{1}' object but received a '{2}'";
    public const string Runtime_Descriptor_NeedsArg = "descriptor '{0}' of '{1}' object needs an argument";

    public const string Runtime_Exception_NonException = "exceptions must be classes or instances deriving from BaseException, not {0}";

    public const string Runtime_ExceptionGroup_NestBaseExceptionsForExceptionGroup = "Cannot nest BaseExceptions in an ExceptionGroup";
    public const string Runtime_ExceptionGroup_NestBaseExceptions = "Cannot nest BaseExceptions in '{0}'";
    public const string Runtime_ExceptionGroup_SplitExpectedCondition = "expected an exception type, a tuple of exception types, or a callable (other than a class)";
    public const string Runtime_ExceptionGroup_DeriveReturnNonGroup = "derive must return an instance of BaseExceptionGroup";
    public const string Runtime_ExceptionGroup_NewGroup_ExcsNonSeq = "second argument (exceptions) must be a sequence";
    public const string Runtime_ExceptionGroup_NewGroup_ExcsEmpty = "second argument (exceptions) must be a non-empty sequence";
    public const string Runtime_ExceptionGroup_NewGroup_ExcsItemNonExc = "Item {0} of second argument (exceptions) is not an exception";
    public const string Runtime_ExceptionGroup_NewGroup_MsgNonStr = "{0}.__new__() argument 1 must be str, not {1}";

    public const string Runtime_Generator_SendNonNoneAtFirst = "can't send non-None value to a just-started generator";
    public const string Runtime_Generator_IgnoredGeneratorExit = "generator ignored GeneratorExit";

    public const string Runtime_Arguments_OverflowArgs = "takes {0} positional arguments but {1} was given";
    public const string Runtime_Arguments_MissingArg = "missing 1 required positional argument";
    public const string Runtime_Arguments_MissingArgs = "missing {0} required positional arguments";
    public const string Runtime_Arguments_UnexpectedKey = "got an unexpected keyword argument '{0}'";
    public const string Runtime_Arguments_MultipleKeywords = "got multiple values for keyword argument '{0}'";

    public const string Runtime_Recursion_MaxRecursionDepthExceeded = "maximum recursion depth exceeded";

    public const string Runtime_Async_NonAwaitable = "'{0}' object can't be awaited";
    public const string Runtime_Async_SendNonNoneAtFirst = "can't send non-None value to a just-started coroutine";
    public const string Runtime_Async_IgnoredGeneratorExit = "coroutine ignored GeneratorExit";
}
