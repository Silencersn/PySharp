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

    _BuildClass,

    PushNull,

    // PySharp only
    _SetupExceptionHandler,
    _EnterFinally,
    _ExitFinally,
    _PopException,

    _UnaryOp,
}
