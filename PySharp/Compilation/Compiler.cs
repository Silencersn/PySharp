using PySharp.AstNodes;
using PySharp.CodeAnalysis;
using PySharp.PyRuntime.Calls;
using PySharp.Tokenization;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Compilation;

internal class Compiler
{
    private static PyAstCompilation InternalCompile(PyCallContext context, string code, string sourceName,
        Func<PyCallContext, CodeSource, IEnumerable<TokenInfo>, AstModNode> parse, bool appendNewLine = false)
    {
        var source = new CodeSource(sourceName, code);
        var tokens = Lexer.Tokenize(context, source);
        if (appendNewLine)
            tokens.Insert(tokens.Count - 1, new TokenInfo(TokenType.NewLine, string.Empty, default, default, source));
        var node = parse(context, source, tokens);
        var model = SemanticAnalyzer.Analyze(context, node);
        return new PyAstCompilation(model);
    }

    public static PyCompilation CompileExec(PyCallContext context, string code, string sourceName)
    {
        return InternalCompile(context, code, sourceName, Parser.ParseModule);
    }

    public static PyCompilation CompileEval(PyCallContext context, string code, string sourceName)
    {
        return InternalCompile(context, code, sourceName, Parser.ParseExpression);
    }

    public static PyCompilation CompileSingle(PyCallContext context, string code, string sourceName, bool appendNewLine)
    {
        return InternalCompile(context, code, sourceName, Parser.ParseInteractive, appendNewLine);
    }
}
