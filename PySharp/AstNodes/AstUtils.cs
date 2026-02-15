using PySharp.CodeAnalysis;
using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.AstNodes;

internal static class AstUtils
{
    public static string GetExprNodeName(AstExprNode exprNode)
    {
        return exprNode switch
        {
            // NamedExprNode => "named expression",
            LambdaNode => "lambda",
            IfExpNode => "conditional expression",
            DictNode => "dict literal",
            SetNode => "set display",
            ListCompNode => "list comprehension",
            DictCompNode => "dict comprehension",
            SetCompNode => "set comprehension",
            GeneratorExpNode => "generator expression",
            // AwaitNode => "await expression",
            YieldNode or YieldFromNode => "yield expression",
            CompareNode => "comparison",
            CallNode => "function call",
            JoinedStrNode => "f-string expression",
            ConstantNode node => node.Value switch
            {
                PyNoneObject => "None",
                PyBoolObject boolObj => boolObj.BoolValue ? "True" : "False", // TODO: __debug__
                _ => "literal"
            },
            AttributeNode => "attribute",
            SubscriptNode => "subscript",
            NameNode => "name",
            ListNode => "list",
            TupleNode => "tuple",
            _ => "expression",
        };
    }

    public static PyObject PyUnwrap(this PyResult result, PyCallContext context)
    {
        if (result.IsError)
            throw new PyRuntimeException(context, result.Exception);

        return result.Value;
    }
    public static TObject PyUnwrap<TObject>(this PyResult<TObject> result, PyCallContext context) where TObject : PyObject
    {
        if (result.IsError)
            throw new PyRuntimeException(context, result.Exception);

        return result.Value;
    }

    public static bool TryGetDoc(IReadOnlyList<AstStmtNode> stmtNodes, [NotNullWhen(true)] out PyStrObject? doc)
    {
        if (stmtNodes.Count > 0 &&
            stmtNodes[0] is ExprNode exprNode &&
            exprNode.Value is ConstantNode constantNode &&
            constantNode.Value is PyStrObject strObj)
        {
            doc = strObj;
            return true;
        }

        doc = null;
        return false;
    }

    public static bool IsValidAugTarget(this AstExprNode node)
    {
        return node is NameNode or SubscriptNode or AttributeNode;
    }

    public static bool IsValidTarget(this AstExprNode node)
    {
        if (IsValidAugTarget(node))
            return true;

        if (node is StarredNode)
            return true;

        if (node is TupleNode tupleNode)
            return tupleNode.Elts.All(IsValidTarget);

        if (node is ListNode listNode)
            return listNode.Elts.All(IsValidTarget);

        return false;
    }

    public static void SetContext(this AstExprNode node, ExprContextType context)
    {
        if (node is not IExprContextNode ctxNode)
            throw new InvalidOperationException();

        ctxNode.Ctx = context;
    }

    public static void CheckValidTargetThenSetContext(this AstExprNode node, ExprContextType context, bool isAugtarget = false)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (isAugtarget)
        {
            if (!IsValidAugTarget(node))
                throw new InvalidOperationException();
        }
        else
        {
            if (!IsValidTarget(node))
                throw new InvalidOperationException();
        }

        SetContext(node, context);
    }

    public static void CheckValidTargetThenSetContext(this IEnumerable<AstExprNode> nodes, ExprContextType context)
    {
        foreach (var node in nodes)
            CheckValidTargetThenSetContext(node, context);
    }

    public static ImmutableArray<T> ToImmutableArray<T>(this IEnumerable<T> source, bool ensureElementsNotNull)
    {
        var array = source.ToImmutableArray();
        if (ensureElementsNotNull)
        {
            for (int i = 0; i < array.Length; i++)
                ArgumentNullException.ThrowIfNull(array[i]);
        }
        return array;
    }

    public static T With<T>(this T node, CodeMetaInfo? metaInfo) where T : AstNode
    {
        node.MetaInfo = metaInfo;
        return node;
    }
}
