namespace PySharp.Compilation.Bytecodes.Extensions;

internal static class BytecodeGeneratorExtensions
{
    public static void Jump(this BytecodeGenerator generator, Label label)
    {
        generator.Emit(OpCode.Jump, label);
    }

    public static void PopJumpIfFalse(this BytecodeGenerator generator, Label label)
    {
        generator.Emit(OpCode.PopJumpIfFalse, label);
    }

    public static void PopJumpIfTrue(this BytecodeGenerator generator, Label label)
    {
        generator.Emit(OpCode.PopJumpIfTrue, label);
    }

    public static void PopJumpIfNone(this BytecodeGenerator generator, Label label)
    {
        generator.Emit(OpCode.PopJumpIfNone, label);
    }
}
