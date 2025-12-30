using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Metadata;

namespace PySharp.AstNodes;

public abstract partial class AstNode : IMetaInfoProvider
{
    public MetaInfo? MetaInfo { get; internal set; }

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
    private readonly bool _trueForStmtFalseForExpr;
    private readonly PyFrame _frame;
    private readonly IMetaInfoProvider? _previous;

    public MetaInfoProviderSetter(PyFrame frame, IMetaInfoProvider provider, bool trueForStmtFalseForExpr)
    {
        _frame = frame;
        if (trueForStmtFalseForExpr)
        {
            _previous = frame.StmtMetaInfoProvider;
            frame.StmtMetaInfoProvider = provider;
        }
        else
        {
            _previous = frame.ExprMetaInfoProvider;
            frame.ExprMetaInfoProvider = provider;
        }
        _trueForStmtFalseForExpr = trueForStmtFalseForExpr;
    }

    void IDisposable.Dispose()
    {
        if (_trueForStmtFalseForExpr)
        {
            _frame.StmtMetaInfoProvider = _previous;
        }
        else
        {
            _frame.ExprMetaInfoProvider = _previous;
        }
    }
}
