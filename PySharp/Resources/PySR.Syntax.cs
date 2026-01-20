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
    
    public const string InvalidSyntax_UnexpectedIndent = "unexpected indent";
    
    public const string InvalidSyntax_Parameters_NoArgsBeforeSlash = "at least one argument must precede /";
    public const string InvalidSyntax_Parameters_ArgsFollowVarKwArg = "arguments cannot follow var-keyword argument";
    public const string InvalidSyntax_Parameters_MultipleSlashes = "/ may appear only once";
    public const string InvalidSyntax_Parameters_SlashAfterStar = "/ must be ahead of *";
    public const string InvalidSyntax_Parameters_MultipleStars = "* may appear only once";
    public const string InvalidSyntax_Parameters_VarKwArgWithDefault = "var-keyword argument cannot have default value";
    public const string InvalidSyntax_Parameters_ParameterWithoutDefault = "parameter without a default follows parameter with a default";
    public const string InvalidSyntax_Parameters_ExpectedDefault = "expected default value expression";

    public const string InvalidSyntax_StarredExpression_Invalid = "Invalid star expression";

    public const string InvalidSyntax_InvalidTarget = "cannot assign to {0}";

    public const string InvalidSyntax_ForStmt_ExpectedIn = "'in' expected after for-loop variables";

    public const string InvalidSyntax_RightParenNeverClosed = "'(' was never closed";

    public const string InvalidSyntax_ExpressionContainsAssignment = "expression cannot contain assignment, perhaps you meant \"==\"?";

    public const string InvalidSyntax_Arguments_PosArgFollowsKeyword = "positional argument follows keyword argument";

}
