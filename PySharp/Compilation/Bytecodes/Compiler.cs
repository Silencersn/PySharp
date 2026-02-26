using PySharp.Compilation.AstNodes;
using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Tokenization;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Compilation.Bytecodes;

internal static class Compiler
{
    private static PyCodeObject InternalCompileBytecode(PyCallContext context, string code, string sourceName,
        Func<PyCallContext, CodeSource, TokenStream, AstModNode> parse, bool appendNewLine = false, bool onlyAsName = false)
    {
        var source = new CodeSource(sourceName, code);
        var tokens = Lexer.Tokenize(context, source);
        if (appendNewLine)
            tokens.Insert(tokens.Count - 1, new TokenInfo(TokenType.NewLine, string.Empty, default, default, source));
        var node = parse(context, source, tokens);
        node = Reducer.Reduce(node);
        var model = SemanticAnalyzer.Analyze(context, node);
        var bytecode = BytecodeCompiler.Compile(model, onlyAsName);
        return new PyCodeObject(sourceName, bytecode);
    }

    public static PyCodeObject CompileExec(PyCallContext context, string code, string sourceName, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, sourceName, Parser.ParseModule, onlyAsName: onlyAsName);
    }

    public static PyCodeObject CompileEval(PyCallContext context, string code, string sourceName, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, sourceName, Parser.ParseExpression, onlyAsName: onlyAsName);
    }

    public static PyCodeObject CompileSingle(PyCallContext context, string code, string sourceName, bool appendNewLine, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, sourceName, Parser.ParseInteractive, appendNewLine, onlyAsName);
    }
}
