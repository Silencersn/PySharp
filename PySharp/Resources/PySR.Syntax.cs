namespace PySharp.Resources;

partial class PySR
{
    public const string InvalidSyntax = "invalid syntax";

    // invalid_fstring_replacement_field

    public const string InvalidSyntax_InvalidFStringReplacementField_BeforeEqual = "f-string: valid expression required before '='";
    public const string InvalidSyntax_InvalidFStringReplacementField_BeforeExclamation = "f-string: valid expression required before '!'";
    public const string InvalidSyntax_InvalidFStringReplacementField_BeforeColon = "f-string: valid expression required before ':'";
    public const string InvalidSyntax_InvalidFStringReplacementField_BeforeRightBrace = "f-string: valid expression required before '}'";

    public const string InvalidSyntax_InvalidFStringReplacementField_ExpectingEqual = "f-string: expecting '=', or '!', or ':', or '}'";
    public const string InvalidSyntax_InvalidFStringReplacementField_ExpectingExclamation = "f-string: expecting '!', or ':', or '}'";
    public const string InvalidSyntax_InvalidFStringReplacementField_ExpectingColon = "f-string: expecting ':' or '}'";
    public const string InvalidSyntax_InvalidFStringReplacementField_ExpectingRightBraceOrSpecs = "f-string: expecting '}', or format specs";
    public const string InvalidSyntax_InvalidFStringReplacementField_ExpectingRightBrace = "f-string: expecting '}'";

    // fstring_conversion
    public const string InvalidSyntax_FStringConversion_Invalid = "f-string: invalid conversion character '{0}': expected 's', 'r', or 'a'";

    // invalid_fstring_conversion_character

    public const string InvalidSyntax_InvalidFStringConversionCharacter_Missing = "f-string: missing conversion character";
    public const string InvalidSyntax_InvalidFStringConversionCharacter_Invalid = "f-string: invalid conversion character";
}
