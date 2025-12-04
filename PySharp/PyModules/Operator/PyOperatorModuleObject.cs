using PySharp.PyModules.Builtins;

namespace PySharp.PyModules.Operator;

public class PyOperatorModuleObject : PyModuleObject
{
    public PyOperatorModuleObject() : base("operator")
    {
        AddObjToAttrs(PyOperatorFunctions.Add); // add
        AddObjToAttrs(PyOperatorFunctions.Sub); // sub
        AddObjToAttrs(PyOperatorFunctions.Mul); // mul
        AddObjToAttrs(PyOperatorFunctions.TrueDiv); // truediv
        AddObjToAttrs(PyOperatorFunctions.FloorDiv); // floordiv
        AddObjToAttrs(PyOperatorFunctions.Mod); // mod
        AddObjToAttrs(PyOperatorFunctions.Pow); // pow
        AddObjToAttrs(PyOperatorFunctions.LShift); // lshift
        AddObjToAttrs(PyOperatorFunctions.RShift); // rshift
        AddObjToAttrs(PyOperatorFunctions.And); // and_
        AddObjToAttrs(PyOperatorFunctions.Xor); // xor
        AddObjToAttrs(PyOperatorFunctions.Or); // or_
        AddObjToAttrs(PyOperatorFunctions.Lt); // lt
        AddObjToAttrs(PyOperatorFunctions.Le); // le
        AddObjToAttrs(PyOperatorFunctions.Eq); // eq
        AddObjToAttrs(PyOperatorFunctions.Ne); // ne
        AddObjToAttrs(PyOperatorFunctions.Gt); // gt
        AddObjToAttrs(PyOperatorFunctions.Ge); // ge
    }
}
