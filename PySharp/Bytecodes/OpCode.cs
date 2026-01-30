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

    StoreName,
    StoreGlobal,
    StoreFast,

    DeleteName,
    DeleteGlobal,
    DeleteFast,

    GetIter,
    ForIter,
    PopIter,

    Call,
    BinaryOp,
    CompareOp,
    ContainsOp,
    IsOp,

    PopTop,

    Copy,
    Swap,

    ToBool,

    Jump,
    PopJumpIfFalse,

    RaiseVarArgs,
    CheckExcMatch,

    _MakeFunctionWithPyArgsDef,
    ReturnValue,

    PushNull,

    // PySharp only
    _SetupExceptionHandler,
    _EnterFinally,
    _ExitFinally,
    _PopException,

    _UnaryOp,
}
