using System.Text.RegularExpressions;

namespace PySharp.Tokenization;

public static partial class LexerRegexes
{
    // some currently unused regexes are commented here to reduce code generated

    //[GeneratedRegex(LexerRegexPatterns.Whitespace)]
    //public static partial Regex Whitespace { get; }

    [GeneratedRegex(LexerRegexPatterns.Comment)]
    public static partial Regex Comment { get; }

    [GeneratedRegex(LexerRegexPatterns.Ignore)]
    public static partial Regex Ignore { get; }

    [GeneratedRegex(LexerRegexPatterns.Name)]
    public static partial Regex Name { get; }

    [GeneratedRegex(LexerRegexPatterns.Number)]
    public static partial Regex Number { get; }

    //[GeneratedRegex(LexerRegexPatterns.Special)]
    //public static partial Regex Special { get; }

    [GeneratedRegex(LexerRegexPatterns.Funny)]
    public static partial Regex Funny { get; }


    [GeneratedRegex(LexerRegexPatterns.Single)]
    public static partial Regex Single { get; }

    [GeneratedRegex(LexerRegexPatterns.Double)]
    public static partial Regex Double { get; }

    [GeneratedRegex(LexerRegexPatterns.Single3)]
    public static partial Regex Single3 { get; }

    [GeneratedRegex(LexerRegexPatterns.Double3)]
    public static partial Regex Double3 { get; }


    //[GeneratedRegex(LexerRegexPatterns.StringPrefix)]
    //public static partial Regex StringPrefix { get; }

    //[GeneratedRegex(LexerRegexPatterns.String)]
    //public static partial Regex String { get; }

    [GeneratedRegex(LexerRegexPatterns.ContStr)]
    public static partial Regex ContStr { get; }


    [GeneratedRegex(LexerRegexPatterns.Token)]
    public static partial Regex Token { get; }


    [GeneratedRegex(LexerRegexPatterns.PseudoExtras)]
    public static partial Regex PseudoExtras { get; }


    [GeneratedRegex(LexerRegexPatterns.PseudoToken)]
    public static partial Regex PseudoToken { get; }
}