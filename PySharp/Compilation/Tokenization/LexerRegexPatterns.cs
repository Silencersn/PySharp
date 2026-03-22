using System.Diagnostics.CodeAnalysis;

namespace PySharp.Compilation.Tokenization;

public static class LexerRegexPatterns
{
    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Whitespace = @"[ \f\t]*";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Comment = @"#[^\r\n]*";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Ignore = @$"{Whitespace}(\\\r?\n{Whitespace})*({Comment})?";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Name = @"\w+";


    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Hexnumber = @"0[xX](?:_?[0-9a-fA-F])+";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Binnumber = @"0[bB](?:_?[01])+";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Octnumber = @"0[oO](?:_?[0-7])+";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Decnumber = @"(?:0(?:_?0)*|[1-9](?:_?[0-9])*)";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Intnumber = @$"({Hexnumber}|{Binnumber}|{Octnumber}|{Decnumber})";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Exponent = @"[eE][-+]?[0-9](?:_?[0-9])*";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Pointfloat = @$"([0-9](?:_?[0-9])*\.(?:[0-9](?:_?[0-9])*)?|\.[0-9](?:_?[0-9])*)({Exponent})?";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Expfloat = $"[0-9](?:_?[0-9])*{Exponent}";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Floatnumber = $"({Pointfloat}|{Expfloat})";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Imagnumber = $"([0-9](?:_?[0-9])*[jJ]|{Floatnumber}[jJ])";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Number = $"({Imagnumber}|{Floatnumber}|{Intnumber})";


    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Special = @"(\~|\}|\|=|\||\{|\^=|\^|\]|\[|@=|@|>>=|>>|>=|>|==|=|<=|<<=|<<|<|;|:=|:|/=|//=|//|/|\.\.\.|\.|\->|\-=|\-|,|\+=|\+|\*=|\*\*=|\*\*|\*|\)|\(|\&=|\&|%=|%|!=|!)";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Funny = @$"(\r?\n|{Special})";


    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Single = @"[^'\\]*(?:\\.[^'\\]*)*'";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Double = @"[^""\\]*(?:\\.[^""\\]*)*""";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Single3 = @"[^'\\]*(?:(?:\\.|'(?!''))[^'\\]*)*'''";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Double3 = @"[^""\\]*(?:(?:\\.|""(?!""""))[^""\\]*)*""""""";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Triple = @$"({StringPrefix}'''|{StringPrefix}"""""")";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string StringPrefix = "(|B|BR|Br|F|FR|Fr|R|RB|RF|RT|Rb|Rf|Rt|T|TR|Tr|U|b|bR|br|f|fR|fr|r|rB|rF|rT|rb|rf|rt|t|tR|tr|u)";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string String = @$"({StringPrefix}'[^\n'\\]*(?:\\.[^\n'\\]*)*'|{StringPrefix}""[^\n""\\]*(?:\\.[^\n""\\]*)*"")";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string PlainToken = $"({Number}|{Funny}|{String}|{Name})";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string Token = Ignore + PlainToken;


    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string ContStr = @$"({StringPrefix}'[^\n'\\]*(?:\\.[^\n'\\]*)*('|\\\r?\n)|{StringPrefix}""[^\n""\\]*(?:\\.[^\n""\\]*)*(""|\\\r?\n))";


    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string PseudoExtras = $@"(\\\r?\n|\Z|{Comment}|{Triple})";

    [StringSyntax(StringSyntaxAttribute.Regex)]
    public const string PseudoToken = Whitespace + $@"({PseudoExtras}|{Number}|{Funny}|{ContStr}|{Name})";
}
