using PySharp.AstNodes;
using PySharp.Bytecodes;
using PySharp.CodeAnalysis;
using PySharp.PyRuntime.Calls;
using PySharp.Tokenization;

namespace PySharp.Compilation;

internal class Compiler
{
    private static PyBytecodeCompilation InternalCompileBytecode(PyCallContext context, string code, string sourceName,
        Func<PyCallContext, CodeSource, IEnumerable<TokenInfo>, AstModNode> parse, bool appendNewLine = false, bool onlyAsName = false)
    {
        var source = new CodeSource(sourceName, code);
        var tokens = Lexer.Tokenize(context, source);
        if (appendNewLine)
            tokens.Insert(tokens.Count - 1, new TokenInfo(TokenType.NewLine, string.Empty, default, default, source));
        var node = parse(context, source, tokens);
        var model = SemanticAnalyzer.Analyze(context, node);
        return BytecodeCompiler.Compile(model, onlyAsName);
    }

    public static PyBytecodeCompilation CompileExec(PyCallContext context, string code, string sourceName, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, sourceName, Parser.ParseModule, onlyAsName: onlyAsName);
    }

    public static PyBytecodeCompilation CompileEval(PyCallContext context, string code, string sourceName, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, sourceName, Parser.ParseExpression, onlyAsName: onlyAsName);
    }

    public static PyBytecodeCompilation CompileSingle(PyCallContext context, string code, string sourceName, bool appendNewLine, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, sourceName, Parser.ParseInteractive, appendNewLine, onlyAsName);
    }

    public static PyBytecodeCompilation CompileBytecode(SemanticModel model)
    {
        return BytecodeCompiler.Compile(model);
    }
}
