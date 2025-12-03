namespace PySharp.AstNodes;

public class AstAliasNode : AstNode
{
    public AstAliasNode(string name, string? asName)
    {
        Name = name;
        AsName = asName;
    }

    public new string Name { get; }
    public string? AsName { get; }
}
