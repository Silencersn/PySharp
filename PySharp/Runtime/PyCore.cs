using PySharp.Compilation.Bytecodes;
using PySharp.Modules.Builtins;
using PySharp.Modules.CSharp;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using System.Diagnostics;

namespace PySharp.Runtime;

internal static class PyCore
{
    public static PyResult Eval(PyCallContext context, Bytecode bytecode)
    {
        var vm = new BytecodeVirtualMachine(context, bytecode);
        return vm.Eval();
    }

    public static IEnumerable<PyCellObject> GetFreeVars(PyFrame frame, PyCodeObject code)
    {
        if (code.FreeVars.Length is 0)
            yield break;

        foreach (var name in code.FreeVars)
        {
            var obj = frame.Variables.Locals[name];
            Debug.Assert(obj is PyCellObject);
            yield return (PyCellObject)obj;
        }
    }

    public static PyFunctionObject MakeFunction(PyFrame frame, PyCodeObject codeObject, PyArgsDef def)
    {
        PyFunctionObject func = null!;
        func = new PyFunctionObject(codeObject.Name, Call, GetFreeVars(frame, codeObject), frame.Variables._globals, codeObject, def);
        return func;

        PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
        {
            if (!def.TryParse(args, kwargs, out var arguments))
                return PyResult.TypeError(null /* TODO */);

            var backFrame = context.CurrentFrame;
            var frame = backFrame.CreateFuncCallFrame(func.Name, func, FrameType.Function, (args, kwargs), func._globals, func.Code);

            Debug.Assert(frame.Variables._locals is not null);
            frame.Variables._locals.InitCells(func.Closure);
            frame.InitArgs(func._def, arguments);

            using var withFrame = context.WithFrame(frame);
            return Eval(context, codeObject.Bytecode);
        }
    }

    public static PyTypeObject BuildClass(PyCallContext context, PyCodeObject codeObject, List<PyTypeObject> bases)
    {
        if (bases.Count is 0)
            bases.Add(PyObjectType.Shared);

        PyTypeObject.ValidateBases(context, bases, out var layoutType);
        var type = UserDefinedType.Create(layoutType, codeObject.Name, codeObject.QualName, bases);

        if (context.CurrentFrame.Variables.Globals.TryGetValue(PySpecialNames.Name, out var module))
            type.ModuleAsObject = module;
        else
            type.ModuleAsObject = PyStrObject.FromString("builtins");

        var newFrame = context.CurrentFrame.CreateClassBuildFrame(type);

        using (var withFrame = context.WithFrame(newFrame))
            Eval(context, codeObject.Bytecode);

        foreach (var pair in newFrame.Variables.Locals)
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
}
