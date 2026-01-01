using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
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

    public static TPyObject PyCast<TPyObject>(this PyObject obj, PyCallContext context)
    {
        if (obj is not TPyObject objOfT)
            throw context.ThrowableTypeError(null);

        return objOfT;
    }

    public static PyObject PyUnwrap(this PyResult result, PyCallContext context)
    {
        if (result.IsError)
            throw new PyRuntimeException(context, result.Exception);

        return result.Value;
    }
    public static PyObject PyUnwrapIncludedNotImplemented(this PyResult result, PyCallContext context)
    {
        if (result.IsError)
            throw new PyRuntimeException(context, result.Exception);

        if (result.IsNotImplemented)
            throw context.ThrowableTypeError(null);

        return result.Value;
    }

    [DoesNotReturn]
    public static void PyThrow(this PyResult result, PyCallContext context)
    {
        Debug.Assert(result.IsError);
        throw new PyRuntimeException(context, result.Exception);
    }

    public static void SetTargetValue(this AstExprNode target, PyCallContext context, PyObject value, PyFrame frame)
    {
        if (target is ITargetNode targetNode)
        {
            targetNode.SetValue(context, value, frame);
        }
        else if (target is TupleNode tupleNode)
        {
            if (!Utils.TryEnumeratedIterable(context, value, out var iter, out var err))
                err.Value.PyThrow(context);

            if (tupleNode.Elts.Length != iter.Count)
            {
                throw context.ThrowableValueError("too many or too few values to unpack");
            }
            for (int i = 0; i < tupleNode.Elts.Length; i++)
            {
                tupleNode.Elts[i].SetTargetValue(context, iter[i], frame);
            }
        }
        else if (target is ListNode listNode)
        {
            if (!Utils.TryEnumeratedIterable(context, value, out var iter, out var err))
                err.Value.PyThrow(context);

            if (listNode.Elts.Length != iter.Count)
            {
                throw context.ThrowableValueError("too many or too few values to unpack");
            }
            for (int i = 0; i < listNode.Elts.Length; i++)
            {
                listNode.Elts[i].SetTargetValue(context, iter[i], frame);
            }
        }
        else
        {
            throw new UnreachableException();
        }
    }

    public static void DeleteTargetValue(this AstExprNode target, PyCallContext context, PyFrame frame)
    {
        if (target is ITargetNode targetNode)
        {
            targetNode.DeleteValue(context, frame);
        }
        else if (target is TupleNode tupleNode)
        {
            foreach (var elt in tupleNode.Elts)
            {
                elt.DeleteTargetValue(context, frame);
            }
        }
        else if (target is ListNode listNode)
        {
            foreach (var elt in listNode.Elts)
            {
                elt.DeleteTargetValue(context, frame);
            }
        }
        else
        {
            throw new UnreachableException();
        }
    }


    public static bool GetBoolValue(this AstExprNode testNode, PyCallContext context, PyFrame frame)
    {
        if (testNode is IAstExprNodeBool node)
            return node.GetExprValueWithResult(context, frame).Result;
        else
            return testNode.GetExprValue(context, frame).Bool(context).PyUnwrap(context).PyCast<PyBoolObject>(context).BoolValue;
    }

    public static void EnumerateNodes(this IEnumerable<AstNode> nodes, Action<AstNode> action)
    {
        foreach (AstNode node in nodes)
        {
            node.EnumerateNodes(action);
        }
    }

    public static PyObject ApplyDecorators(PyObject target, List<AstExprNode> decoratorList, PyCallContext context, PyFrame frame)
    {
        if (decoratorList.Count > 0)
        {
            Stack<PyObject> decorators = [];
            foreach (var decorator in decoratorList)
            {
                decorators.Push(decorator.GetExprValue(context, frame));
            }
            foreach (var decorator in decorators)
            {
                target = decorator.Call(context, [target], new Dictionary<string, PyObject>()).PyUnwrap(context);
            }
        }
        return target;
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
}
