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
    public static TObject PyUnwrap<TObject>(this PyResult<TObject> result, PyCallContext context) where TObject : PyObject
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
        else if (target is StarredNode starredNode)
        {
            throw new NotImplementedException();
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
            return PySpecialMethods.Bool(context, testNode.GetExprValue(context, frame)).PyUnwrap(context).PyCast<PyBoolObject>(context).BoolValue;
    }

    public static PyObject ApplyDecorators(PyObject target, ImmutableArray<AstExprNode> decoratorList, PyCallContext context, PyFrame frame)
    {
        if (decoratorList.Length > 0)
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

    public static bool IsValidAugTarget(this AstExprNode node)
    {
        //return node is NameNode or SubscriptNode or AttributeNode;
        return node is ITargetNode;
    }

    public static bool IsValidTarget(this AstExprNode node)
    {
        if (IsValidAugTarget(node))
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

    public static List<PyObject> EvalPyObjects(PyCallContext context, PyFrame frame, IEnumerable<AstExprNode> exprs)
    {
        List<PyObject> result = [];
        foreach (var expr in exprs)
        {
            if (expr is StarredNode starredNode)
            {
                result.AddRange(starredNode.Unpack(context, frame));
            }
            else
            {
                var value = expr.GetExprValue(context, frame);
                result.Add(value);
            }
        }
        return result;
    }

    public static Dictionary<string, PyObject> EvalKeywords(PyCallContext context, PyFrame frame, IEnumerable<AstKeywordNode> keywords)
    {
        Dictionary<string, PyObject> result = [];
        foreach (var keyword in keywords)
            keyword.AddOrUnpackValueTo(result, context, frame);
        return result;
    }
}
