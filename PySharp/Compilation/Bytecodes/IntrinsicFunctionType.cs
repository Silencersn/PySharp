namespace PySharp.Compilation.Bytecodes;

internal enum IntrinsicFunctionType
{
    Invalid = 0,
    ListToTuple,
    Print,
    ImportStar,
    TypeVar,
    MakeAnnotateFunc,
}
