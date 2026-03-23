namespace PySharp.Compilation.Bytecodes;

internal enum OpCode : byte
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
    __CallImpl,

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
    GetAwaitable,
    _CheckExcToRaise,
    Send,

    _BuildClass,
    _LoadClass,

    MakeCell,
    _MakeCellFast,

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

    ExtendedArg,

    LoadMethod,

    BuildTemplate,
    BuildInterpolation,

    __BytecodeEnd,

    __LabelFlag = 0b10000000,
}
