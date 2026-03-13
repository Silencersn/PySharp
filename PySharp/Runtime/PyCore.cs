using PySharp.Compilation.Bytecodes;
using PySharp.Compilation.Primitives;
using PySharp.Modules;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Diagnostics;

namespace PySharp.Runtime;

internal static class PyCore
{
    public static PyResult Eval(PyCallContext context, Bytecode bytecode, bool usingLocalsPlusAsOperandStack = false)
    {
        var vmStates = new BytecodeVirtualMachineStates(context, bytecode, usingLocalsPlusAsOperandStack);
        return BytecodeVirtualMachine.Eval(ref vmStates);
    }

    public static PyCellObject[]? GetFreeVars(ref PyInternalFrame frame, PyCodeObject code)
    {
        if (code.FreeVars.Length is 0)
            return null;

        var cells = new PyCellObject[code.FreeVars.Length];

        var variables = frame.Variables;
        var span = variables.LocalsSpan;
        var table = variables.LocalsTable;
        for (int i = 0; i < code.FreeVars.Length; i++)
        {
            var name = code.FreeVars[i];

            PyObject obj;
            if (table.TryGetValue(name, out var index))
            {
                // function

                Debug.Assert(span[index] is not null);
                obj = span[index]!;
            }
            else
            {
                // class

                Debug.Assert(variables.Locals[name] is not null);
                obj = variables.Locals[name]!;
            }
            Debug.Assert(obj is PyCellObject);
            cells[i] = (PyCellObject)obj;
        }

        return cells;
    }

    public static PyFunctionObject MakeFunction(ref PyInternalFrame frame, PyCodeObject codeObject, PyArgsDef def)
    {
        return new PyFunctionObject(GetFreeVars(ref frame, codeObject), frame.Variables.Globals, codeObject, def);
    }

    public static PyTypeObject BuildClass(PyCallContext context, PyCodeObject codeObject, List<PyTypeObject> bases)
    {
        if (bases.Count is 0)
            bases.Add(PyObjectType.Shared);

        PyTypeObject.ValidateBases(context, bases, out var layoutTypeOwner);
        var type = layoutTypeOwner.CreateUserDefinedTypeWithSameLayout(codeObject.Name, codeObject.QualName, bases);

        if (context.CurrentInternalFrame.Variables.GlobalsDict.TryGetValue(PySpecialNames.Name, out var module))
            type.ModuleAsObject = module;
        else
            type.ModuleAsObject = PyStrObject.FromString("builtins");

        var newFrame = context.CurrentInternalFrame.CreateClassBuildFrame(type, codeObject);

        using (var withFrame = context.WithFrame(ref newFrame))
            // TODO: unwrap
            Eval(context, codeObject.Bytecode);

        foreach (var pair in newFrame.Variables.EnumerateVariablesForBuildingClass())
        {
            if (pair.Value is null)
                continue;

            type.PyAttributes[pair.Key] = pair.Value;
        }

        foreach (var (name, value) in type.PyAttributes)
        {
            var setNameFunc = value.PyType.Slots.SetName;
            if (setNameFunc is not null)
                setNameFunc(context, value, type, PyStrObject.FromString(name)).PyUnwrap(context);

            switch (name)
            {
                case PySpecialNames.New: type.Slots.New = value.ToClsArgsKwargsFunction(); break;

                case PySpecialNames.Str: type.Slots.Str = value.ToUnaryFunction(); break;
                case PySpecialNames.Repr: type.Slots.Repr = value.ToUnaryFunction(); break;
                case PySpecialNames.Bool: type.Slots.Bool = value.ToUnaryFunction(); break;
                case PySpecialNames.Hash: type.Slots.Hash = value.ToUnaryFunction(); break;
                case PySpecialNames.Len: type.Slots.Len = value.ToUnaryFunction(); break;
                case PySpecialNames.Index: type.Slots.Index = value.ToUnaryFunction(); break;
                case PySpecialNames.Int: type.Slots.Int = value.ToUnaryFunction(); break;
                case PySpecialNames.Float: type.Slots.Float = value.ToUnaryFunction(); break;
                case PySpecialNames.Call: type.Slots.Call = value.ToSelfArgsKwargsFunction(); break;

                case PySpecialNames.Iter: type.Slots.Iter = value.ToUnaryFunction(); break;
                case PySpecialNames.Next: type.Slots.Next = value.ToUnaryFunction(); break;
                case PySpecialNames.GetItem: type.Slots.GetItem = value.ToBinaryFunction(); break;
                case PySpecialNames.SetItem: type.Slots.SetItem = value.ToTernaryFunction(); break;
                case PySpecialNames.DelItem: type.Slots.DelItem = value.ToBinaryFunction(); break;
                case PySpecialNames.Contains: type.Slots.Contains = value.ToBinaryFunction(); break;

                case PySpecialNames.Enter: type.Slots.Enter = value.ToUnaryFunction(); break;
                case PySpecialNames.Exit: type.Slots.Exit = value.ToQuaternaryFunction(); break;

                case PySpecialNames.Get: type.Slots.Get = value.ToTernaryFunction(); break;
                case PySpecialNames.Set: type.Slots.Set = value.ToTernaryFunction(); break;
                case PySpecialNames.Delete: type.Slots.Delete = value.ToBinaryFunction(); break;
                case PySpecialNames.GetAttribute: type.Slots.GetAttribute = value.ToBinaryFunction(); break;
                case PySpecialNames.GetAttr: type.Slots.GetAttr = value.ToBinaryFunction(); break;
                case PySpecialNames.SetAttr: type.Slots.SetAttr = value.ToTernaryFunction(); break;
                case PySpecialNames.DelAttr: type.Slots.DelAttr = value.ToBinaryFunction(); break;

                // Binary operators
                case PySpecialNames.Add: type.Slots.Add = value.ToBinaryFunction(); break;
                case PySpecialNames.Sub: type.Slots.Sub = value.ToBinaryFunction(); break;
                case PySpecialNames.Mul: type.Slots.Mul = value.ToBinaryFunction(); break;
                case PySpecialNames.TrueDiv: type.Slots.TrueDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.FloorDiv: type.Slots.FloorDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.Mod: type.Slots.Mod = value.ToBinaryFunction(); break;
                case PySpecialNames.DivMod: type.Slots.DivMod = value.ToBinaryFunction(); break;
                case PySpecialNames.LShift: type.Slots.LShift = value.ToBinaryFunction(); break;
                case PySpecialNames.RShift: type.Slots.RShift = value.ToBinaryFunction(); break;
                case PySpecialNames.And: type.Slots.And = value.ToBinaryFunction(); break;
                case PySpecialNames.Xor: type.Slots.Xor = value.ToBinaryFunction(); break;
                case PySpecialNames.Or: type.Slots.Or = value.ToBinaryFunction(); break;

                // Reverse binary operators
                case PySpecialNames.RAdd: type.Slots.RAdd = value.ToBinaryFunction(); break;
                case PySpecialNames.RSub: type.Slots.RSub = value.ToBinaryFunction(); break;
                case PySpecialNames.RMul: type.Slots.RMul = value.ToBinaryFunction(); break;
                case PySpecialNames.RTrueDiv: type.Slots.RTrueDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.RFloorDiv: type.Slots.RFloorDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.RMod: type.Slots.RMod = value.ToBinaryFunction(); break;
                case PySpecialNames.RDivMod: type.Slots.RDivMod = value.ToBinaryFunction(); break;
                case PySpecialNames.RLShift: type.Slots.RLShift = value.ToBinaryFunction(); break;
                case PySpecialNames.RRShift: type.Slots.RRShift = value.ToBinaryFunction(); break;
                case PySpecialNames.RAnd: type.Slots.RAnd = value.ToBinaryFunction(); break;
                case PySpecialNames.RXor: type.Slots.RXor = value.ToBinaryFunction(); break;
                case PySpecialNames.ROr: type.Slots.ROr = value.ToBinaryFunction(); break;

                // In-place binary operators
                case PySpecialNames.IAdd: type.Slots.IAdd = value.ToBinaryFunction(); break;
                case PySpecialNames.ISub: type.Slots.ISub = value.ToBinaryFunction(); break;
                case PySpecialNames.IMul: type.Slots.IMul = value.ToBinaryFunction(); break;
                case PySpecialNames.IMatMul: type.Slots.IMatMul = value.ToBinaryFunction(); break;
                case PySpecialNames.ITrueDiv: type.Slots.ITrueDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.IFloorDiv: type.Slots.IFloorDiv = value.ToBinaryFunction(); break;
                case PySpecialNames.IMod: type.Slots.IMod = value.ToBinaryFunction(); break;
                case PySpecialNames.ILShift: type.Slots.ILShift = value.ToBinaryFunction(); break;
                case PySpecialNames.IRShift: type.Slots.IRShift = value.ToBinaryFunction(); break;
                case PySpecialNames.IAnd: type.Slots.IAnd = value.ToBinaryFunction(); break;
                case PySpecialNames.IXor: type.Slots.IXor = value.ToBinaryFunction(); break;
                case PySpecialNames.IOr: type.Slots.IOr = value.ToBinaryFunction(); break;

                // Ternary operators
                case PySpecialNames.Pow: type.Slots.Pow = value.ToTernaryFunction(); break;
                case PySpecialNames.RPow: type.Slots.RPow = value.ToTernaryFunction(); break;
                case PySpecialNames.IPow: type.Slots.IPow = value.ToTernaryFunction(); break;

                // Rich comparison operators
                case PySpecialNames.Lt: type.Slots.Lt = value.ToBinaryFunction(); break;
                case PySpecialNames.Le: type.Slots.Le = value.ToBinaryFunction(); break;
                case PySpecialNames.Eq: type.Slots.Eq = value.ToBinaryFunction(); break;
                case PySpecialNames.Ne: type.Slots.Ne = value.ToBinaryFunction(); break;
                case PySpecialNames.Gt: type.Slots.Gt = value.ToBinaryFunction(); break;
                case PySpecialNames.Ge: type.Slots.Ge = value.ToBinaryFunction(); break;

                case PySpecialNames.Complex: type.Slots.Complex = value.ToUnaryFunction(); break;
                case PySpecialNames.Abs: type.Slots.Abs = value.ToUnaryFunction(); break;
                case PySpecialNames.Neg: type.Slots.Neg = value.ToUnaryFunction(); break;
                case PySpecialNames.Pos: type.Slots.Pos = value.ToUnaryFunction(); break;
                case PySpecialNames.Invert: type.Slots.Invert = value.ToUnaryFunction(); break;
                case PySpecialNames.SetName: type.Slots.SetName = value.ToTernaryFunction(); break;
                case PySpecialNames.Missing: type.Slots.Missing = value.ToBinaryFunction(); break;
                case PySpecialNames.Init: type.Slots.Init = value.ToSelfArgsKwargsFunction(); break;
                case PySpecialNames.Format: type.Slots.Format = value.ToBinaryFunction(); break;
            }
        }
        return type;
    }

    public static void ImportAllFrom(PyCallContext context, ref PyInternalFrame frame, PyModuleObject module)
    {
        // if module has __all__, import only those names
        // item in __all__ must be str
        if (module.PyAttributes.TryGetValue(PySpecialNames.All, out var all))
        {
            // unlike cpython, allows iterable
            var list = PyUtils.IterableToList(context, all).PyUnwrap(context);

            foreach (var item in list)
            {
                if (item is not PyStrObject strObj)
                    throw context.TypeError(PySR.Runtime_Import_NonStringAllElt, module.Name, item.PyType.Name);

                var attr = PyOperators.GetAttr(context, module, strObj.Value).PyUnwrap(context);
                frame.Variables.StoreName(strObj.Value, attr).PyUnwrap(context);
            }
        }
        else
        {
            foreach (var kvp in module.PyAttributes)
            {
                // only import names that do not start with '_'
                if (!kvp.Key.StartsWith('_'))
                    frame.Variables.StoreName(kvp.Key, kvp.Value).PyUnwrap(context);
            }
        }
    }

    public static void Raise(PyCallContext context, ref PyInternalFrame frame, PyObject? excObj, PyObject? causeObj)
    {
        var exc = ToException(context, excObj)
            ?? throw new PyRuntimeException(context, frame.CurrentException);

        if (causeObj is not null)
        {
            if (causeObj is PyNoneObject)
            {
                exc.SuppressContext = true;
            }
            else
            {
                exc.Cause = ToException(context, causeObj);
                exc.CauseReason = PySR.Runtime_RaiseStmt_Cause;
            }
        }

        if (frame.Exceptions.TryPeek(out var pre))
            exc.Context = pre;

        throw new PyRuntimeException(context, exc);

        static PyExceptionObject? ToException(PyCallContext context, PyObject? pyObj)
        {
            if (pyObj is null)
                return null;

            if (pyObj is PyExceptionObject excObj)
                return excObj;

            else if (pyObj is PyTypeObject typeObj && typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                return new PyExceptionObject(typeObj, []);

            else
                throw context.TypeError(PySR.Runtime_RaiseStmt_RaiseNonException);
        }
    }

    public static Func<PyExceptionObject, bool> MakeExceptCondition(PyCallContext context, PyObject type)
    {
        if (type is PyTypeObject typeObj)
        {
            if (!typeObj.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);

            return typeObj.IsInstance;
        }
        else if (type is PyTupleObject tupleObj)
        {
            if (!tupleObj.All(obj => obj is PyTypeObject t && t.IsSubclassOf(PyBaseExceptionObjectType.Shared)))
                throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);

            return exc => tupleObj.Any(obj => ((PyTypeObject)obj).IsInstance(exc));
        }
        else
        {
            throw context.TypeError(PySR.Runtime_TryStmt_CatchNonException);
        }
    }

    public static (PyExceptionObject? RestExc, PyObject MatchedExc) SplitExceptionGroup(PyCallContext context, PyExceptionObject exception, PyObject type)
    {
        var splitResult = exception.CallMethod(context, "split", [type]).PyUnwrap(context);
        if (splitResult is not PyTupleObject tuple)
            throw context.TypeError(PySR.Runtime_TryStmt_SplitReturnsNonTuple, exception.PyType.FullName, splitResult.PyType.FullName);

        if (tuple.Count is not 2)
            throw context.TypeError(PySR.Runtime_TryStmt_SplitReturnsTupleWithWrongSize, exception.PyType.FullName, tuple.Count);

        var match = tuple[0];
        var restObj = tuple[1];
        var rest = restObj is PyNoneObject ? null : (restObj as PyExceptionObject) ??
            throw context.TypeError(PySR.Runtime_TryStmt_ExpectedExceptionOrNone, tuple[1].PyType.FullName);

        return (rest, match);
    }

    public static PyResult EvalOperator(PyCallContext context, OperatorType op, PyObject left, PyObject right)
    {
        return op switch
        {
            OperatorType.Add => PyOperators.Add(context, left, right),
            OperatorType.Sub => PyOperators.Sub(context, left, right),
            OperatorType.Mult => PyOperators.Mult(context, left, right),
            OperatorType.MatMult => throw new NotImplementedException(), // PyOperators.MatMult(context, left, right),
            OperatorType.Div => PyOperators.TrueDiv(context, left, right),
            OperatorType.Mod => PyOperators.Mod(context, left, right),
            OperatorType.Pow => PyOperators.Pow(context, left, right, PyNoneObject.None),
            OperatorType.LShift => PyOperators.LShift(context, left, right),
            OperatorType.RShift => PyOperators.RShift(context, left, right),
            OperatorType.BitOr => PyOperators.BitOr(context, left, right),
            OperatorType.BitXor => PyOperators.BitXor(context, left, right),
            OperatorType.BitAnd => PyOperators.BitAnd(context, left, right),
            OperatorType.FloorDiv => PyOperators.FloorDiv(context, left, right),
            _ => throw new UnreachableException(),
        };
    }

    public static PyResult EvalOperator(PyCallContext context, UnaryOpType op, PyObject value)
    {
        return op switch
        {
            UnaryOpType.Invert => PyOperators.Invert(context, value),
            UnaryOpType.Not => PyOperators.Not(context, value),
            UnaryOpType.UAdd => PyOperators.UAdd(context, value),
            UnaryOpType.USub => PyOperators.USub(context, value),
            _ => throw new UnreachableException(),
        };
    }

    public static PyResult EvalOperator(PyCallContext context, CmpopType op, PyObject left, PyObject right)
    {
        return op switch
        {
            CmpopType.Eq => PyOperators.Eq(context, left, right),
            CmpopType.NotEq => PyOperators.NotEq(context, left, right),
            CmpopType.Lt => PyOperators.Lt(context, left, right),
            CmpopType.LtE => PyOperators.LtE(context, left, right),
            CmpopType.Gt => PyOperators.Gt(context, left, right),
            CmpopType.GtE => PyOperators.GtE(context, left, right),
            CmpopType.Is => PyOperators.Is(left, right),
            CmpopType.IsNot => PyOperators.IsNot(left, right),
            CmpopType.In => PyOperators.In(context, left, right),
            CmpopType.NotIn => PyOperators.NotIn(context, left, right),
            _ => throw new UnreachableException(),
        };
    }

    public static PyResult EvalInplaceOperator(PyCallContext context, OperatorType op, PyObject left, PyObject right)
    {
        return op switch
        {
            OperatorType.Add => PyOperators.InPlaceAdd(context, left, right),
            OperatorType.Sub => PyOperators.InPlaceSub(context, left, right),
            OperatorType.Mult => PyOperators.InPlaceMult(context, left, right),
            OperatorType.MatMult => throw new NotImplementedException(), // PyOperators.InPlaceMatMult(context, left, right),
            OperatorType.Div => PyOperators.InPlaceTrueDiv(context, left, right),
            OperatorType.Mod => PyOperators.InPlaceMod(context, left, right),
            OperatorType.Pow => PyOperators.InPlacePow(context, left, right, PyNoneObject.None),
            OperatorType.LShift => PyOperators.InPlaceLShift(context, left, right),
            OperatorType.RShift => PyOperators.InPlaceRShift(context, left, right),
            OperatorType.BitOr => PyOperators.InPlaceBitOr(context, left, right),
            OperatorType.BitXor => PyOperators.InPlaceBitXor(context, left, right),
            OperatorType.BitAnd => PyOperators.InPlaceBitAnd(context, left, right),
            OperatorType.FloorDiv => PyOperators.InPlaceFloorDiv(context, left, right),
            _ => throw new UnreachableException(),
        };
    }

    internal static bool IsSequenceForMatch(PyObject obj)
    {
        return obj switch
        {
            PyListObject or
            PyTupleObject or
            PyRangeObject => true,

            PyStrObject => false, // str is not regarded as sequence

            _ => false,// TODO: support other valid sequences
        };
    }

    internal static bool IsMappingForMatch(PyObject obj)
    {
        return obj switch
        {
            PyDictObject => true,
            _ => false,// TODO: support other valid mapping
        };
    }

    internal static PyResult GetAttrOrMethod(PyCallContext context, PyObject self, string name, out bool isMethod)
    {
        isMethod = false;
        if (!ReferenceEquals(self.PyType.Slots.GetAttribute, PyObjectType.GenericGetAttribute))
            return PyOperators.GetAttr(context, self, name);

        var type = self.PyType;

        if (name is PySpecialNames.Class)
            return type;

        if (PyObject.TryLookupAttrInMro(type, name, out var attr))
        {
            if (Utils.IsDataDescriptor(attr))
            {
                var getFunc = attr.PyType.Slots.Get;
                if (getFunc is not null)
                    return getFunc(context, attr, self, type);
            }
        }

        if (self._pyAttributes?.TryGetValue(name, out var value) is true)
            return value;

        if (attr is not null)
        {
            if (attr is PyFunctionObject or PyWrapperDescriptorObject)
            {
                isMethod = true;
                return attr;
            }

            var getFunc = attr.PyType.Slots.Get;
            if (getFunc is not null)
                return getFunc(context, attr, self, type);

            return attr;
        }

        return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, self.PyType.FullName, name);
    }
}
