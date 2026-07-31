using PySharp.Compilation.AstNodes;
using PySharp.Compilation.Bytecodes;
using PySharp.Compilation.CodeAnalysis;
using PySharp.Compilation.Tokenization;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Compilation;

public enum CompileMode
{
    Exec,
    Eval,
    Single
}

public static class Compiler
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

    internal static PyCodeObject InternalCompileExec(PyCallContext context, string code, string filename, string name, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, filename, name, Parser.ParseModule, onlyAsName: onlyAsName);
    }

    internal static PyCodeObject InternalCompileEval(PyCallContext context, string code, string filename, string name, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, filename, name, Parser.ParseExpression, onlyAsName: onlyAsName);
    }

    internal static PyCodeObject InternalCompileSingle(PyCallContext context, string code, string filename, string name, bool appendNewLine, bool onlyAsName = false)
    {
        return InternalCompileBytecode(context, code, filename, name, Parser.ParseInteractive, appendNewLine, onlyAsName);
    }

    public static PyCodeObject? Compile(string code, CompileMode mode, PyCallContext? context, string? filename = null)
    {
        const string Name = "<module>";

        context ??= PyCallContext.CreateFromEnvironment();
        filename ??= "<string>";
        try
        {
            return mode switch
            {
                CompileMode.Exec => InternalCompileExec(context, code, filename, Name),
                CompileMode.Eval => InternalCompileEval(context, code, filename, Name),
                CompileMode.Single => InternalCompileSingle(context, code, filename, Name, appendNewLine: true),
                _ => throw new NotSupportedException()
            };
        }
        catch (PyRuntimeException)
        {
            return null;
        }
    }
}
