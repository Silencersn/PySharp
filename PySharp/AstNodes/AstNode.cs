using PySharp.CodeAnalysis;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.AstNodes;

public abstract partial class AstNode : ICodeMetaInfoProvider
{
    public CodeMetaInfo? MetaInfo { get; internal set; }

    bool ICodeMetaInfoProvider.OnlyStartInfo => this is AstStmtNode;

    public virtual void Execute(PyCallContext context, PyFrame frame)
    {
        throw new NotSupportedException();
    }

    public override string ToString()
    {
        var dumper = new AstNodeDumper();
        Dump(dumper);
        return dumper.ToString();
    }

    internal virtual void Dump(AstNodeDumper dumper)
    {
        throw new NotImplementedException();
    }

    public virtual void EnumerateNodes(Action<AstNode> action)
    {
        action(this);
    }
}

public interface IAstNodeLocation
{
    public int? Lineno => null;
    public int? ColOffset => null;
    public int? EndLineno => null;
    public int? EndColOffset => null;
}


internal readonly struct MetaInfoProviderSetter : IDisposable
{
    private readonly PyFrame _frame;
    private readonly ICodeMetaInfoProvider? _previous;

    public MetaInfoProviderSetter(PyFrame frame, ICodeMetaInfoProvider provider)
    {
        _frame = frame;
        _previous = _frame.MetaInfoProvider;
        _frame.MetaInfoProvider = provider;
    }

    void IDisposable.Dispose()
    {
        _frame.MetaInfoProvider = _previous;
    }
}
