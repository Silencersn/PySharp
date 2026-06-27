namespace PySharp.Compilation.Bytecodes.Extensions;

internal static class BytecodeBuilderExtensions
{
    public static void Jump(this BytecodeBuilder builder, Label label)
    {
        builder.Emit(OpCode.Jump, label);
    }

    public static void PopJumpIfFalse(this BytecodeBuilder builder, Label label)
    {
        builder.Emit(OpCode.PopJumpIfFalse, label);
    }

    public static void PopJumpIfTrue(this BytecodeBuilder builder, Label label)
    {
        builder.Emit(OpCode.PopJumpIfTrue, label);
    }

    public static void PopJumpIfNone(this BytecodeBuilder builder, Label label)
    {
        builder.Emit(OpCode.PopJumpIfNone, label);
    }
}
