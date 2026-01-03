using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.AstNodes;

public abstract class AstExceptHandlerNode : AstNode
{
    public abstract bool TryHandle(PyCallContext context, PyFrame frame, PyExceptionObject exception);
}

public class ExceptHandlerNode : AstExceptHandlerNode
{
    public ExceptHandlerNode(AstExprNode? type, string? name)
    {
        Type = type;
        Name = name;
    }

    public AstExprNode? Type { get; }
    public string? Name { get; }
    public List<AstStmtNode> Body { get; } = [];

    public override bool TryHandle(PyCallContext context, PyFrame frame, PyExceptionObject exception)
    {
        if (IsMatch())
        {
            if (Name is not null)
                frame.SetVariable(Name, exception).PyUnwrap(context);

            foreach (var stmt in Body)
            {
                stmt.Execute(context, frame);
            }

            if (Name is not null)
                frame.DeleteVariable(Name).PyUnwrap(context);

            return true;
        }

        return false;

        bool IsMatch()
        {
            if (Type is null)
                return true;

            if (Type.GetExprValue(context, frame) is not PyTypeObject typeObj || !typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                throw context.ThrowableTypeError("catching classes that do not inherit from BaseException is not allowed");

            return typeObj.IsInstance(exception);
        }
    }

    public override IEnumerable<AstNode> EnumerateSubNodes()
    {
        if (Type is not null)
            yield return Type;
        foreach (var stmt in Body)
            yield return stmt;
    }
}