namespace PySharp.AstNodes;

public abstract class AstExprContextNode : AstNode;

public class LoadNode : AstExprContextNode;
public class StoreNode : AstExprContextNode;
public class DelNode : AstExprContextNode;
