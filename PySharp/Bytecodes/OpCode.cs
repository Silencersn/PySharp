namespace PySharp.Bytecodes;

internal enum OpCode
{
    NoOperation = 0,

    LoadConst,
    LoadSpecial,
    _LoadExcInfo,
    _LoadHitExcept,

    LoadName,
    LoadGlobal,
    LoadFast,
    LoadDeref,
    _LoadDerefFast,

    StoreName,
    StoreGlobal,
    StoreFast,
    StoreDeref,
    _StoreDerefFast,
    _StoreNameIncludedNonInlineFrame,
    _StoreDerefIncludedNonInlineFrame,

    DeleteName,
    DeleteGlobal,
    DeleteFast,
    DeleteDeref,
    _DeleteDerefFast,

    LoadAttr,
    StoreAttr,
    DeleteAttr,

    GetIter,
    ForIter,
    PopIter,

    Call,
    CallKw,
    CallFunctionEx,
    BinaryOp,
    CompareOp,
    ContainsOp,
    IsOp,

    _AugAssignOp,

    PopTop,

    Copy,
    Swap,

    ToBool,

    Jump,
    PopJumpIfFalse,
    PopJumpIfTrue,
    PopJumpIfNone,

    RaiseVarArgs,
    CheckExcMatch,
    CheckEgMatch,
    _CheckMatch,
    _LoadExc,

    _MakeFunctionWithPyArgsDef,
    ReturnValue,
    ReturnGenerator,
    YieldValue,
    GetYieldFromIter,
    _CheckExcToRaise,
    Send,

    _BuildClass,
    _LoadClass,

    MakeCell,

    PushNull,

    BuildList,
    BuildTuple,
    BuildSet,
    BuildMap,
    BuildSlice,

    _ListToTuple,
    _ListToSet,

    _EnterInlineFrame,
    _ExitInlineFrame,
    ListAppend,
    ListExtend,
    SetAdd,
    MapAdd,
    DictUpdate,
    DictMerge,
    _MakeGeneratorExp,

    UnpackSequence,
    UnpackEx,

    BinarySubscr,
    StoreSubscr,
    DeleteSubscr,

    ConvertValue,
    FormatSimple,
    FormatWithSpec,
    BuildString,

    MatchSequence,
    MatchMapping,
    GetLen,
    MatchKeys,
    MatchClass,

    ImportName,
    ImportFrom,
    _ImportAllFrom,

    // PySharp only
    _SetupFinally,
    _SetupExcept,
    _EnterFinally,
    _ExitFinally,
    _PopException,
    _PopExceptionIfTrue,
    _PopExceptionAndJumpIfNull,

    _UnaryOp,
    UnaryNot,

    _CallPrintIfNotNone,
}
