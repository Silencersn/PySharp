using PySharp.Modules.Builtins;

namespace PySharp.Modules.Mathematics;

public class PyMathModuleObject : PyModuleObject
{
    public PyMathModuleObject() : base("math")
    {
        AddObjToAttrs("pi", PyFloatObject.Pi);
        AddObjToAttrs("e", PyFloatObject.E);
        AddObjToAttrs("tau", PyFloatObject.Tau);

        AddObjToAttrs(PyMathFunctions.Sqrt);
        AddObjToAttrs(PyMathFunctions.Acos);
        AddObjToAttrs(PyMathFunctions.Asin);
        AddObjToAttrs(PyMathFunctions.Atan);
        AddObjToAttrs(PyMathFunctions.Cos);
        AddObjToAttrs(PyMathFunctions.Sin);
        AddObjToAttrs(PyMathFunctions.Tan);
        AddObjToAttrs(PyMathFunctions.Exp);
        AddObjToAttrs(PyMathFunctions.Acosh);
        AddObjToAttrs(PyMathFunctions.Asinh);
        AddObjToAttrs(PyMathFunctions.Atanh);
        AddObjToAttrs(PyMathFunctions.Cosh);
        AddObjToAttrs(PyMathFunctions.Sinh);
        AddObjToAttrs(PyMathFunctions.Tanh);
        AddObjToAttrs(PyMathFunctions.Fabs);
        AddObjToAttrs(PyMathFunctions.Ceil);
        AddObjToAttrs(PyMathFunctions.Floor);
        AddObjToAttrs(PyMathFunctions.Trunc);
        AddObjToAttrs(PyMathFunctions.Remainder);
        AddObjToAttrs(PyMathFunctions.Atan2);
        AddObjToAttrs(PyMathFunctions.Copysign);
        AddObjToAttrs(PyMathFunctions.Fmod);
        AddObjToAttrs(PyMathFunctions.Pow);
        AddObjToAttrs(PyMathFunctions.Gcd);
        AddObjToAttrs(PyMathFunctions.Lcm);
    }
}
