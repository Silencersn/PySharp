using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;

namespace PySharp.AstNodes;

public abstract class AstExceptHandlerNode : AstNode
{
    public abstract bool TryHandle(PyFrame frame, PyExceptionObject exception);
}

public class ExceptHandlerNode : AstExceptHandlerNode
{
    public ExceptHandlerNode(AstExprNode? type, string? identifier)
    {
        Type = type;
        Identifier = identifier;
    }

    public AstExprNode? Type { get; }
    public string? Identifier { get; }
    public List<AstStmtNode> Body { get; } = [];

    public override bool TryHandle(PyFrame frame, PyExceptionObject exception)
    {
        if (Type?.GetExprValue(frame) is not PyExceptionType type)
        {
            PyVirtualMachine.RaiseTypeError(null);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        if (Type is null || type is not null && type.IsInstance(exception))
        {
            if (Identifier is not null)
                frame.SetValue(Identifier, exception);

            foreach (var stmt in Body)
            {
                stmt.Execute(frame);
            }

            if (Identifier is not null)
                frame.RemoveValue(Identifier);

            return true;
        }

        return false;
    }
}