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



