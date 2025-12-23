using PySharp.PyModules;
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.AstNodes;

internal static class AstUtils
{
    public static TPyObject PyCast<TPyObject>(this PyObject? obj)
    {
        obj.PyThrowIfNull();

        if (obj is not TPyObject objOfT)
        {
            PyVirtualMachine.RaiseTypeError(null);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        return objOfT;
    }

    public static TPyObject PyThrowIfNull<TPyObject>([NotNull] this TPyObject? obj) where TPyObject : PyObject
    {
        if (obj is null)
            throw new PyRuntimeException(PyVirtualMachine.CurrentException ?? throw new NotImplementedException("No Current Exception"));
        return obj;
    }

    public static PyObject PyUnwrap(this PyResult result)
    {
        if (result.IsError)
            throw new PyRuntimeException(result.Exception);

        return result.Value;
    }
    public static PyObject PyUnwrapIncludedNotImplemented(this PyResult result)
    {
        if (result.IsError)
            throw new PyRuntimeException(result.Exception);

        if (result.IsNotImplemented)
        {
            PyVirtualMachine.RaiseTypeError(null);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        return result.Value;
    }

    [DoesNotReturn]
    public static void PyThrow(this PyResult result)
    {
        Debug.Assert(result.IsError);
        throw new PyRuntimeException(result.Exception);
    }

    public static PyObject PyThrowIfNullOrNotImplemented([NotNull] this PyObject? obj)
    {
        if (obj is null)
            throw new PyRuntimeException(PyVirtualMachine.CurrentException ?? throw new NotImplementedException());

        if (obj is PyNotImplementedObject)
        {
            PyVirtualMachine.RaiseTypeError(null);
            throw new PyRuntimeException(PyVirtualMachine.CurrentException);
        }

        return obj;
    }

    public static PyExceptionType PyCastExceptionType(this PyObject? obj)
    {
        obj.PyThrowIfNull();
        if (obj is PyExceptionType objectType)
            return objectType;
        PyVirtualMachine.RaiseTypeError("exceptions must derive from BaseException");
        throw new PyRuntimeException(PyVirtualMachine.CurrentException);
    }

    public static void SetTargetValue(this AstExprNode target, PyCallContext context, PyObject value, PyFrame frame)
    {
        if (target is ITargetNode targetNode)
        {
            targetNode.SetVaue(context, value, frame);
        }
        else if (target is TupleNode tupleNode)
        {
            if (!Utils.TryEnumeratedIterable(context, value, out var iter, out var err))
                err.Value.PyThrow();

            if (tupleNode.Elts.Length != iter.Count)
            {
                PyVirtualMachine.RaiseValueError("too many or too few values to unpack");
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }
            for (int i = 0; i < tupleNode.Elts.Length; i++)
            {
                tupleNode.Elts[i].SetTargetValue(context, iter[i], frame);
            }
        }
        else if (target is ListNode listNode)
        {
            if (!Utils.TryEnumeratedIterable(context, value, out var iter, out var err))
                err.Value.PyThrow();

            if (listNode.Elts.Length != iter.Count)
            {
                PyVirtualMachine.RaiseValueError("too many or too few values to unpack");
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }
            for (int i = 0; i < listNode.Elts.Length; i++)
            {
                listNode.Elts[i].SetTargetValue(context, iter[i], frame);
            }
        }
        else
        {
            Debug.Fail("???");
            throw new NotSupportedException();
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
            Debug.Fail("???");
            throw new NotSupportedException();
        }
    }


    public static bool GetBoolValue(this AstExprNode testNode, PyCallContext context, PyFrame frame)
    {
        if (testNode is IAstExprNodeBool node)
            return node.GetExprValueWithResult(context, frame).Result;
        else
            return testNode.GetExprValue(context, frame).Bool(context).PyUnwrap().PyCast<PyBoolObject>().BoolValue;
    }

    public static void EnumerateNodes(this IEnumerable<AstNode> nodes, Action<AstNode> action)
    {
        foreach (AstNode node in nodes)
        {
            node.EnumerateNodes(action);
        }
    }

    public static PyObject ApplyDeractors(PyObject target, List<AstExprNode> decoratorList, PyCallContext context, PyFrame frame)
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
                target = decorator.Call(context, [target], new Dictionary<string, PyObject>()).PyUnwrap();
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
