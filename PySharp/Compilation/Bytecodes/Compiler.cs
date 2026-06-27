using PySharp.Compilation.AstNodes;
using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Tokenization;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;

namespace PySharp.Compilation.Bytecodes;

internal static class Compiler
{
    private static PyCodeObject InternalCompileBytecode(PyCallContext context, string code, string filename, string name,
        Func<PyCallContext, CodeSource, TokenSequence, bool, AstModNode> parse, bool appendNewLine = false, bool onlyAsName = false)
    {
        var source = new CodeSource(filename, code);
        var tokens = Lexer.Tokenize(context, source, appendNewLine);
        var node = parse(context, source, tokens, true);
        var model = SemanticAnalyzer.Analyze(context, source, node);
        var bytecode = Emitter.Emit(context, model, source, onlyAsName);
        return new PyCodeObject(name, filename, bytecode, CodeObjectFlags.Module);
    }

    public static PyCodeObject CompileExec(PyCallContext context, string code, string filename, string name, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, filename, name, Parser.ParseModule, onlyAsName: onlyAsName);
    }

    public static PyCodeObject CompileEval(PyCallContext context, string code, string filename, string name, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, filename, name, Parser.ParseExpression, onlyAsName: onlyAsName);
    }

    public static PyCodeObject CompileSingle(PyCallContext context, string code, string filename, string name, bool appendNewLine, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, filename, name, Parser.ParseInteractive, appendNewLine, onlyAsName);
    }
}
