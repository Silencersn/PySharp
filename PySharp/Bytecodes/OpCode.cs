using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Bytecodes;

internal enum OpCode
{
    NoOperation = 0,

    LoadConst,
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

    RaiseVarArgs,
    CheckExcMatch,
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

    _EnterInlineFrame,
    _ExitInlineFrame,
    ListAppend,
    SetAdd,
    MapAdd,
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

    // PySharp only
    _SetupExceptionHandler,
    _EnterFinally,
    _ExitFinally,
    _PopException,

    _UnaryOp,
}
