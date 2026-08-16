namespace PySharp.Compilation.Bytecodes;

internal enum IntrinsicFunctionType
{
    Invalid = 0,
    ListToTuple,
    _ListToSet,
    Print,
    ImportStar,
    TypeVar,
}
