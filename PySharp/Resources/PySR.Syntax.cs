namespace PySharp.Resources;

partial class PySR
{
    public const string InvalidSyntax = "invalid syntax";

    public const string InvalidSyntax_Warning_InvalidEscapeSequence = @"""\{0}"" is an invalid escape sequence. Such sequences will not work in the future. Did you mean ""\\{0}""? A raw string is also an option.";

    public const string InvalidSyntax_FString_ReplacementField_BeforeEqual = "f-string: valid expression required before '='";
    public const string InvalidSyntax_FString_ReplacementField_BeforeExclamation = "f-string: valid expression required before '!'";
    public const string InvalidSyntax_FString_ReplacementField_BeforeColon = "f-string: valid expression required before ':'";
    public const string InvalidSyntax_FString_ReplacementField_BeforeRightBrace = "f-string: valid expression required before '}'";
    public const string InvalidSyntax_FString_ReplacementField_ExpectingEqual = "f-string: expecting '=', or '!', or ':', or '}'";
    public const string InvalidSyntax_FString_ReplacementField_ExpectingExclamation = "f-string: expecting '!', or ':', or '}'";
    public const string InvalidSyntax_FString_ReplacementField_ExpectingColon = "f-string: expecting ':' or '}'";
    public const string InvalidSyntax_FString_ReplacementField_ExpectingRightBraceOrSpecs = "f-string: expecting '}', or format specs";
    public const string InvalidSyntax_FString_ReplacementField_ExpectingRightBrace = "f-string: expecting '}'";

    public const string InvalidSyntax_FString_ConversionCharacter_Missing = "f-string: missing conversion character";
    public const string InvalidSyntax_FString_ConversionCharacter_Invalid = "f-string: invalid conversion character";
    public const string InvalidSyntax_FString_ConversionCharacter_InvalidCharacter = "f-string: invalid conversion character '{0}': expected 's', 'r', or 'a'";

    public const string InvalidSyntax_AssignmentExpressions = "cannot use assignment expressions with {0}";
    
    public const string InvalidSyntax_UnicodeError_TruncatedLowerXSequence = "(unicode error) 'unicodeescape' codec can't decode bytes in position {0}-{1}: truncated \\xXX escape";
    public const string InvalidSyntax_UnicodeError_TruncatedLowerUSequence = "(unicode error) 'unicodeescape' codec can't decode bytes in position {0}-{1}: truncated \\uXXXX escape";
    public const string InvalidSyntax_UnicodeError_TruncatedUpperUSequence = "(unicode error) 'unicodeescape' codec can't decode bytes in position {0}-{1}: truncated \\UXXXXXXXX escape";
    public const string InvalidSyntax_UnicodeError_IllegalCharacter = "(unicode error) 'unicodeescape' codec can't decode bytes in position {0}-{1}: illegal Unicode character";
    
    public const string InvalidSyntax_Indentation_Unexpected = "unexpected indent";
    public const string InvalidSyntax_Indentation_ExpectedForBlock = "expected an indented block after {0} on line {1}";
    
    public const string InvalidSyntax_Parameters_NoArgsBeforeSlash = "at least one argument must precede /";
    public const string InvalidSyntax_Parameters_ArgsFollowVarKwArg = "arguments cannot follow var-keyword argument";
    public const string InvalidSyntax_Parameters_MultipleSlashes = "/ may appear only once";
    public const string InvalidSyntax_Parameters_SlashAfterStar = "/ must be ahead of *";
    public const string InvalidSyntax_Parameters_MultipleStars = "* may appear only once";
    public const string InvalidSyntax_Parameters_VarKwArgWithDefault = "var-keyword argument cannot have default value";
    public const string InvalidSyntax_Parameters_ParameterWithoutDefault = "parameter without a default follows parameter with a default";
    public const string InvalidSyntax_Parameters_ExpectedDefault = "expected default value expression";

    public const string InvalidSyntax_StarredExpression_Invalid = "Invalid star expression";
    public const string InvalidSyntax_StarredExpression_CannotUseHere = "can't use starred expression here";

    public const string InvalidSyntax_InvalidTarget = "cannot assign to {0}";

    public const string InvalidSyntax_ForStmt_ExpectedIn = "'in' expected after for-loop variables";

    public const string InvalidSyntax_RightParenNeverClosed = "'(' was never closed";

    public const string InvalidSyntax_ExpressionContainsAssignment = "expression cannot contain assignment, perhaps you meant \"==\"?";

    public const string InvalidSyntax_Arguments_PosArgFollowsKeyword = "positional argument follows keyword argument";

    public const string InvalidSyntax_InvalidAugAssignTarget = "'{0}' is an illegal expression for augmented assignment";

    public const string InvalidSyntax_CannotDeleteStarred = "cannot delete starred";

    public const string InvalidSyntax_TryStmt_ExpectedExceptOrFinally = "expected 'except' or 'finally' block";
    public const string InvalidSyntax_TryStmt_BothExceptAndExceptStar = "cannot have both 'except' and 'except*' on the same 'try'";
    public const string InvalidSyntax_TryStmt_MultipleExceptionTypesUsingAs = "multiple exception types must be parenthesized when using 'as'";
    public const string InvalidSyntax_TryStmt_ExpectedExceptionTypes = "expected one or more exception types";

    public const string InvalidSyntax_TypeParam_BoundForTypeVarTuple = "cannot use bound with TypeVarTuple";
    public const string InvalidSyntax_TypeParam_BoundForParamSpec = "cannot use bound with ParamSpec";
    
    public const string InvalidSyntax_Pattern_InvalidPatternTarget = "cannot use {0} as pattern target";
    public const string InvalidSyntax_Pattern_UnderscoreAsTarget = "cannot use '_' as a target";
    public const string InvalidSyntax_Pattern_RealNumberRequired = "real number required in complex literal";
    public const string InvalidSyntax_Pattern_ImaginaryNumberRequired = "imaginary number required in complex literal";
}
