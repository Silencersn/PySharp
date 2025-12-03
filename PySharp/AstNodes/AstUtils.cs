using PySharp.PyObjects;
using PySharp.PyObjects.Builtins;
using PySharp.PyRuntime;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

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
            throw new PyRuntimeException(PyVirtualMachine.CurrentException ?? throw new NotImplementedException());
        return obj;
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

    public static void SetTargetValue(this AstExprNode target, PyObject value, PyFrame frame)
    {
        if (target is ITargetNode targetNode)
        {
            targetNode.SetVaue(value, frame);
        }
        else if (target is TupleNode tupleNode)
        {
            var iter = Utils.EnumerabledIterable(value);
            if (iter is null)
            {
                Debug.Assert(PyVirtualMachine.CurrentException is not null);
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }

            if (tupleNode.Elts.Length != iter.Count)
            {
                PyVirtualMachine.RaiseValueError("too many or too few values to unpack");
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }
            for (int i = 0; i < tupleNode.Elts.Length; i++)
            {
                tupleNode.Elts[i].SetTargetValue(iter[i], frame);
            }
        }
        else if (target is ListNode listNode)
        {
            var iter = Utils.EnumerabledIterable(value);
            if (iter is null)
            {
                Debug.Assert(PyVirtualMachine.CurrentException is not null);
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }
            if (listNode.Elts.Length != iter.Count)
            {
                PyVirtualMachine.RaiseValueError("too many or too few values to unpack");
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }
            for (int i = 0; i < listNode.Elts.Length; i++)
            {
                listNode.Elts[i].SetTargetValue(iter[i], frame);
            }
        }
        else
        {
            Debug.Fail("???");
            throw new NotSupportedException();
        }
    }

    public static void DeleteTargetValue(this AstExprNode target, PyFrame frame)
    {
        if (target is ITargetNode targetNode)
        {
            targetNode.DeleteValue(frame);
        }
        else if (target is TupleNode tupleNode)
        {
            foreach (var elt in tupleNode.Elts)
            {
                elt.DeleteTargetValue(frame);
            }
        }
        else if (target is ListNode listNode)
        {
            foreach (var elt in listNode.Elts)
            {
                elt.DeleteTargetValue(frame);
            }
        }
        else
        {
            Debug.Fail("???");
            throw new NotSupportedException();
        }
    }


    public static bool GetBoolValue(this AstExprNode testNode, PyFrame frame)
    {
        if (testNode is IAstExprNodeBool node)
            return node.GetExprValueWithResult(frame).Result;
        else
            return testNode.GetExprValue(frame).Bool().PyCast<PyBoolObject>().BoolValue;
    }

    public static void EnumerateNodes(this IEnumerable<AstNode> nodes, Action<AstNode> action)
    {
        foreach (AstNode node in nodes)
        {
            node.EnumerateNodes(action);
        }
    }

    public static Dictionary<string, PyFrame> CaptureFrames(PyFrame frame, Dictionary<string, PyVariableType> variables)
    {
        Dictionary<string, PyFrame> capturedFrames = [];
        foreach (var closureName in variables.Where(pair => pair.Value is PyVariableType.Closure).Select(pair => pair.Key))
        {
            var f = frame;
            while (f is not null)
            {
                if (f._variables is null)
                {
                    f = f.Back;
                    continue;
                }

                if (f._variables.TryGetValue(closureName, out var type) && type is not PyVariableType.Closure)
                {
                    capturedFrames[closureName] = f;
                    break;
                }

                f = f.Back;
            }
            if (!capturedFrames.ContainsKey(closureName))
                Debug.Fail("why");
        }
        return capturedFrames;
    }

    public static PyObject ApplyDeractors(PyObject target, List<AstExprNode> decoratorList, PyFrame frame)
    {
        if (decoratorList.Count > 0)
        {
            Stack<PyObject> decorators = [];
            foreach (var decorator in decoratorList)
            {
                decorators.Push(decorator.GetExprValue(frame));
            }
            foreach (var decorator in decorators)
            {
                target = decorator.Call([target], new Dictionary<string, PyObject>()).PyThrowIfNull();
            }
        }
        return target;
    }
}
