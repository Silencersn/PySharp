using System;
using System.Collections.Generic;
using System.Text;

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

    StoreName,
    StoreGlobal,
    StoreFast,
    StoreDeref,
    _StoreNameIncludedNonInlineFrame,
    _StoreDerefIncludedNonInlineFrame,

    DeleteName,
    DeleteGlobal,
    DeleteFast,
    DeleteDeref,

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
    _SetupExceptionHandler,
    _EnterFinally,
    _ExitFinally,
    _PopException,
    _PopExceptionIfTrue,
    _PopExceptionAndJumpIfNull,

    _UnaryOp,
}
