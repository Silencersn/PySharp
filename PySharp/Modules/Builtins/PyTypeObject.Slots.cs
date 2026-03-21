using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject
{
    protected internal PyTypeSlots Slots { get; }

    protected internal sealed class PyTypeSlots
    {
        // TODO: support different protocols


        internal PyClsArgsKwargsFunction? New;

        internal PyUnaryFunction? Str;
        internal PyUnaryFunction? Repr;
        internal PyUnaryFunction? Bool;
        internal PyUnaryFunction? Hash;
        internal PyUnaryFunction? Len;
        internal PyUnaryFunction? Index;
        internal PyUnaryFunction? Int;
        internal PyUnaryFunction? Float;
        internal PySelfArgsKwargsFunction? Call;

        internal PyUnaryFunction? Iter;
        internal PyUnaryFunction? Next;
        internal PyBinaryFunction? GetItem;
        internal PyTernaryFunction? SetItem;
        internal PyBinaryFunction? DelItem;
        internal PyBinaryFunction? Contains;

        internal PyUnaryFunction? Enter;
        internal PyQuaternaryFunction? Exit;

        internal PyTernaryFunction? Get;
        internal PyTernaryFunction? Set;
        internal PyBinaryFunction? Delete;
        internal PyBinaryFunction? GetAttribute;
        internal PyBinaryFunction? GetAttr;
        internal PyTernaryFunction? SetAttr;
        internal PyBinaryFunction? DelAttr;

        // Binary operators
        internal PyBinaryFunction? Add;
        internal PyBinaryFunction? Sub;
        internal PyBinaryFunction? Mul;
        internal PyBinaryFunction? TrueDiv;
        internal PyBinaryFunction? FloorDiv;
        internal PyBinaryFunction? Mod;
        internal PyBinaryFunction? DivMod;
        internal PyBinaryFunction? LShift;
        internal PyBinaryFunction? RShift;
        internal PyBinaryFunction? And;
        internal PyBinaryFunction? Xor;
        internal PyBinaryFunction? Or;

        // Rich comparison operators
        internal PyBinaryFunction? Lt;
        internal PyBinaryFunction? Le;
        internal PyBinaryFunction? Eq;
        internal PyBinaryFunction? Ne;
        internal PyBinaryFunction? Gt;
        internal PyBinaryFunction? Ge;

        // Reverse binary operators
        internal PyBinaryFunction? RAdd;
        internal PyBinaryFunction? RSub;
        internal PyBinaryFunction? RMul;
        internal PyBinaryFunction? RTrueDiv;
        internal PyBinaryFunction? RFloorDiv;
        internal PyBinaryFunction? RMod;
        internal PyBinaryFunction? RDivMod;
        internal PyBinaryFunction? RLShift;
        internal PyBinaryFunction? RRShift;
        internal PyBinaryFunction? RAnd;
        internal PyBinaryFunction? RXor;
        internal PyBinaryFunction? ROr;

        // In-place binary operators
        internal PyBinaryFunction? IAdd;
        internal PyBinaryFunction? ISub;
        internal PyBinaryFunction? IMul;
        internal PyBinaryFunction? IMatMul;
        internal PyBinaryFunction? ITrueDiv;
        internal PyBinaryFunction? IFloorDiv;
        internal PyBinaryFunction? IMod;
        internal PyTernaryFunction? IPow;
        internal PyBinaryFunction? ILShift;
        internal PyBinaryFunction? IRShift;
        internal PyBinaryFunction? IAnd;
        internal PyBinaryFunction? IXor;
        internal PyBinaryFunction? IOr;

        // Ternary operators
        internal PyTernaryFunction? Pow;
        internal PyTernaryFunction? RPow;

        internal PyUnaryFunction? Complex;
        internal PyUnaryFunction? Abs;
        internal PyUnaryFunction? Neg;
        internal PyUnaryFunction? Pos;
        internal PyUnaryFunction? Invert;
        internal PyTernaryFunction? SetName;
        internal PyBinaryFunction? Missing;
        internal PySelfArgsKwargsFunction? Init;
        internal PyBinaryFunction? Format;

        internal PyUnaryFunction? Await;

        internal PyUnaryFunction? Reversed;

        internal PyTypeSlots Clone()
        {
            return (PyTypeSlots)MemberwiseClone();
        }

        internal void FillNullWith(PyTypeSlots other)
        {
            New ??= other.New;

            Str ??= other.Str;
            Repr ??= other.Repr;
            Bool ??= other.Bool;
            Hash ??= other.Hash;
            Len ??= other.Len;
            Index ??= other.Index;
            Int ??= other.Int;
            Float ??= other.Float;
            Call ??= other.Call;

            Iter ??= other.Iter;
            Next ??= other.Next;
            GetItem ??= other.GetItem;
            SetItem ??= other.SetItem;
            DelItem ??= other.DelItem;
            Contains ??= other.Contains;

            Enter ??= other.Enter;
            Exit ??= other.Exit;

            Get ??= other.Get;
            Set ??= other.Set;
            Delete ??= other.Delete;
            GetAttribute ??= other.GetAttribute;
            GetAttr ??= other.GetAttr;
            SetAttr ??= other.SetAttr;
            DelAttr ??= other.DelAttr;

            Add ??= other.Add;
            Sub ??= other.Sub;
            Mul ??= other.Mul;
            TrueDiv ??= other.TrueDiv;
            FloorDiv ??= other.FloorDiv;
            Mod ??= other.Mod;
            DivMod ??= other.DivMod;
            LShift ??= other.LShift;
            RShift ??= other.RShift;
            And ??= other.And;
            Xor ??= other.Xor;
            Or ??= other.Or;

            Lt ??= other.Lt;
            Le ??= other.Le;
            Eq ??= other.Eq;
            Ne ??= other.Ne;
            Gt ??= other.Gt;
            Ge ??= other.Ge;

            RAdd ??= other.RAdd;
            RSub ??= other.RSub;
            RMul ??= other.RMul;
            RTrueDiv ??= other.RTrueDiv;
            RFloorDiv ??= other.RFloorDiv;
            RMod ??= other.RMod;
            RDivMod ??= other.RDivMod;
            RLShift ??= other.RLShift;
            RRShift ??= other.RRShift;
            RAnd ??= other.RAnd;
            RXor ??= other.RXor;
            ROr ??= other.ROr;

            IAdd ??= other.IAdd;
            ISub ??= other.ISub;
            IMul ??= other.IMul;
            IMatMul ??= other.IMatMul;
            ITrueDiv ??= other.ITrueDiv;
            IFloorDiv ??= other.IFloorDiv;
            IMod ??= other.IMod;
            IPow ??= other.IPow;
            ILShift ??= other.ILShift;
            IRShift ??= other.IRShift;
            IAnd ??= other.IAnd;
            IXor ??= other.IXor;
            IOr ??= other.IOr;

            Pow ??= other.Pow;
            RPow ??= other.RPow;

            Complex ??= other.Complex;
            Abs ??= other.Abs;
            Neg ??= other.Neg;
            Pos ??= other.Pos;
            Invert ??= other.Invert;
            SetName ??= other.SetName;
            Missing ??= other.Missing;
            Init ??= other.Init;
            Format ??= other.Format;

            Await ??= other.Await;
            Reversed ??= other.Reversed;
        }

        internal static PyTypeSlots Create(IEnumerable<PyTypeObject> types)
        {
            var slots = new PyTypeSlots();
            foreach (var type in types)
                slots.FillNullWith(type.Slots);
            return slots;
        }
    }
}
