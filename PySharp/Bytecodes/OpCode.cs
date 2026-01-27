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

    Call,

    PopTop,

    Copy,

    ToBool,

    Jump,
    PopJumpIfFalse,
}
