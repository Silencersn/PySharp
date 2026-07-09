"""
Test for ExtendedArg jump target bug in StackSizeHelper.

This test creates functions with conditional jumps (POP_JUMP_IF_FALSE)
whose target offsets exceed 255 bytecode instructions. The StackSizeHelper
incorrectly uses only the low 8 bits of the jump target, ignoring ExtendedArg
preceding the jump instruction.

The bug is in StackSizeHelper.InternalCalculate:
    int nextTarget = instruction.Arg;  // ❌ only low 8 bits!

When this bug is present, the stack size calculation may be wrong, potentially
causing runtime IndexOutOfRangeException if the operand stack is undersized.
"""

# ============================================================
# Test 1: Long if-body (>256 instructions) with runtime condition
# ============================================================

def test_long_if(should_run):
    """Function whose if-body generates >256 bytecode instructions,
    forcing POP_JUMP_IF_FALSE to use ExtendedArg for its jump target.

    'should_run' is a runtime parameter so the compiler cannot constant-fold it.
    """
    r = 0
    step = 0

    if should_run:
        # Each line below generates ~4 bytecode instructions.
        # With ~150 lines, the if-body exceeds 256 instructions,
        # making the POP_JUMP_IF_FALSE target require ExtendedArg.

        r = r + 1; step = step + 1
        r = r + 2; step = step + 1
        r = r + 3; step = step + 1
        r = r + 4; step = step + 1
        r = r + 5; step = step + 1
        r = r + 6; step = step + 1
        r = r + 7; step = step + 1
        r = r + 8; step = step + 1
        r = r + 9; step = step + 1
        r = r + 10; step = step + 1

        r = r + 11; step = step + 1
        r = r + 12; step = step + 1
        r = r + 13; step = step + 1
        r = r + 14; step = step + 1
        r = r + 15; step = step + 1
        r = r + 16; step = step + 1
        r = r + 17; step = step + 1
        r = r + 18; step = step + 1
        r = r + 19; step = step + 1
        r = r + 20; step = step + 1

        r = r + 21; step = step + 1
        r = r + 22; step = step + 1
        r = r + 23; step = step + 1
        r = r + 24; step = step + 1
        r = r + 25; step = step + 1
        r = r + 26; step = step + 1
        r = r + 27; step = step + 1
        r = r + 28; step = step + 1
        r = r + 29; step = step + 1
        r = r + 30; step = step + 1

        r = r + 31; step = step + 1
        r = r + 32; step = step + 1
        r = r + 33; step = step + 1
        r = r + 34; step = step + 1
        r = r + 35; step = step + 1
        r = r + 36; step = step + 1
        r = r + 37; step = step + 1
        r = r + 38; step = step + 1
        r = r + 39; step = step + 1
        r = r + 40; step = step + 1

        r = r + 41; step = step + 1
        r = r + 42; step = step + 1
        r = r + 43; step = step + 1
        r = r + 44; step = step + 1
        r = r + 45; step = step + 1
        r = r + 46; step = step + 1
        r = r + 47; step = step + 1
        r = r + 48; step = step + 1
        r = r + 49; step = step + 1
        r = r + 50; step = step + 1

        r = r + 51; step = step + 1
        r = r + 52; step = step + 1
        r = r + 53; step = step + 1
        r = r + 54; step = step + 1
        r = r + 55; step = step + 1
        r = r + 56; step = step + 1
        r = r + 57; step = step + 1
        r = r + 58; step = step + 1
        r = r + 59; step = step + 1
        r = r + 60; step = step + 1

        r = r + 61; step = step + 1
        r = r + 62; step = step + 1
        r = r + 63; step = step + 1
        r = r + 64; step = step + 1
        r = r + 65; step = step + 1
        r = r + 66; step = step + 1
        r = r + 67; step = step + 1
        r = r + 68; step = step + 1
        r = r + 69; step = step + 1
        r = r + 70; step = step + 1

        r = r + 71; step = step + 1
        r = r + 72; step = step + 1
        r = r + 73; step = step + 1
        r = r + 74; step = step + 1
        r = r + 75; step = step + 1
        r = r + 76; step = step + 1
        r = r + 77; step = step + 1
        r = r + 78; step = step + 1
        r = r + 79; step = step + 1
        r = r + 80; step = step + 1

        r = r + 81; step = step + 1
        r = r + 82; step = step + 1
        r = r + 83; step = step + 1
        r = r + 84; step = step + 1
        r = r + 85; step = step + 1
        r = r + 86; step = step + 1
        r = r + 87; step = step + 1
        r = r + 88; step = step + 1
        r = r + 89; step = step + 1
        r = r + 90; step = step + 1

        r = r + 91; step = step + 1
        r = r + 92; step = step + 1
        r = r + 93; step = step + 1
        r = r + 94; step = step + 1
        r = r + 95; step = step + 1
        r = r + 96; step = step + 1
        r = r + 97; step = step + 1
        r = r + 98; step = step + 1
        r = r + 99; step = step + 1
        r = r + 100; step = step + 1

        r = r + 101; step = step + 1
        r = r + 102; step = step + 1
        r = r + 103; step = step + 1
        r = r + 104; step = step + 1
        r = r + 105; step = step + 1
        r = r + 106; step = step + 1
        r = r + 107; step = step + 1
        r = r + 108; step = step + 1
        r = r + 109; step = step + 1
        r = r + 110; step = step + 1

        r = r + 111; step = step + 1
        r = r + 112; step = step + 1
        r = r + 113; step = step + 1
        r = r + 114; step = step + 1
        r = r + 115; step = step + 1
        r = r + 116; step = step + 1
        r = r + 117; step = step + 1
        r = r + 118; step = step + 1
        r = r + 119; step = step + 1
        r = r + 120; step = step + 1

        r = r + 121; step = step + 1
        r = r + 122; step = step + 1
        r = r + 123; step = step + 1
        r = r + 124; step = step + 1
        r = r + 125; step = step + 1
        r = r + 126; step = step + 1
        r = r + 127; step = step + 1
        r = r + 128; step = step + 1
        r = r + 129; step = step + 1
        r = r + 130; step = step + 1

        r = r + 131; step = step + 1
        r = r + 132; step = step + 1
        r = r + 133; step = step + 1
        r = r + 134; step = step + 1
        r = r + 135; step = step + 1
        r = r + 136; step = step + 1
        r = r + 137; step = step + 1
        r = r + 138; step = step + 1
        r = r + 139; step = step + 1
        r = r + 140; step = step + 1

        r = r + 141; step = step + 1
        r = r + 142; step = step + 1
        r = r + 143; step = step + 1
        r = r + 144; step = step + 1
        r = r + 145; step = step + 1
        r = r + 146; step = step + 1
        r = r + 147; step = step + 1
        r = r + 148; step = step + 1
        r = r + 149; step = step + 1
        r = r + 150; step = step + 1

    return r, step


# Run Test 1
result_r, result_step = test_long_if(True)
assert result_r == 11325, f"Test 1 True: expected 11325, got {result_r}"
assert result_step == 150, f"Test 1 True: expected 150, got {result_step}"

result_r, result_step = test_long_if(False)
assert result_r == 0, f"Test 1 False: expected 0, got {result_r}"
assert result_step == 0, f"Test 1 False: expected 0, got {result_step}"


# ============================================================
# Test 2: Long for-loop body triggering FOR_ITER ExtendedArg bug
# ============================================================
def test_long_for():
    """Function with a long for-loop body so that FOR_ITER's exhaust-jump
    target requires ExtendedArg (>255 instructions from FOR_ITER)."""
    total = 0
    for i in range(5):
        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

        total = total + i * 2
        total = total - i // 3
        total = total + i ** 2
        total = total % 9973
        total = total + i * 3
        total = total - i // 2
        total = total + i ** 3
        total = total % 9973

    expected_for = 1740
    assert total == expected_for, f"Test 2: expected {expected_for}, got {total}"
    return total

test_long_for()


# ============================================================
# Test 3: if-else with both branches long (>256 instructions each)
#          The else-branch has the higher stack depth.
#          If StackSizeHelper's jump target is truncated to 8 bits,
#          the else-branch's start may never be enqueued, leading to
#          an undersized operand stack and potential crash.
# ============================================================
def test_if_else_deep(should_run):
    """If-else with both branches >256 instructions.
    The else-branch builds a 50-element tuple (many simultaneous stack
    pushes), creating a higher peak stack depth than the if-branch.

    With the bug, StackSizeHelper may not analyze the else-branch,
    producing too small a StackSize and risking runtime crash."""
    if should_run:
        # If-branch: fast path, simple arithmetic, ~140 instructions
        x = 0
        x = x + 1; x = x + 2; x = x + 3; x = x + 4; x = x + 5
        x = x + 6; x = x + 7; x = x + 8; x = x + 9; x = x + 10
        x = x + 11; x = x + 12; x = x + 13; x = x + 14; x = x + 15
        x = x + 16; x = x + 17; x = x + 18; x = x + 19; x = x + 20
        x = x + 21; x = x + 22; x = x + 23; x = x + 24; x = x + 25
        x = x + 26; x = x + 27; x = x + 28; x = x + 29; x = x + 30
        x = x + 31; x = x + 32; x = x + 33; x = x + 34; x = x + 35
        x = x + 36; x = x + 37; x = x + 38; x = x + 39; x = x + 40
        x = x + 41; x = x + 42; x = x + 43; x = x + 44; x = x + 45
        x = x + 46; x = x + 47; x = x + 48; x = x + 49; x = x + 50
        x = x + 51; x = x + 52; x = x + 53; x = x + 54; x = x + 55
        x = x + 56; x = x + 57; x = x + 58; x = x + 59; x = x + 60
        x = x + 61; x = x + 62; x = x + 63; x = x + 64; x = x + 65
        x = x + 66; x = x + 67; x = x + 68; x = x + 69; x = x + 70
        x = x + 71; x = x + 72; x = x + 73; x = x + 74; x = x + 75
        x = x + 76; x = x + 77; x = x + 78; x = x + 79; x = x + 80
        x = x + 81; x = x + 82; x = x + 83; x = x + 84; x = x + 85
        x = x + 86; x = x + 87; x = x + 88; x = x + 89; x = x + 90
        x = x + 91; x = x + 92; x = x + 93; x = x + 94; x = x + 95
        x = x + 96; x = x + 97; x = x + 98; x = x + 99; x = x + 100
        return x, 1
    else:
        # Else-branch: same arithmetic, but also builds 50-element tuple
        # BUILD_TUPLE 50 pushes 50 items then pops them all, creating high peak
        x = 0
        x = x + 1; x = x + 2; x = x + 3; x = x + 4; x = x + 5
        x = x + 6; x = x + 7; x = x + 8; x = x + 9; x = x + 10
        x = x + 11; x = x + 12; x = x + 13; x = x + 14; x = x + 15
        x = x + 16; x = x + 17; x = x + 18; x = x + 19; x = x + 20
        x = x + 21; x = x + 22; x = x + 23; x = x + 24; x = x + 25
        x = x + 26; x = x + 27; x = x + 28; x = x + 29; x = x + 30
        x = x + 31; x = x + 32; x = x + 33; x = x + 34; x = x + 35
        x = x + 36; x = x + 37; x = x + 38; x = x + 39; x = x + 40
        x = x + 41; x = x + 42; x = x + 43; x = x + 44; x = x + 45
        x = x + 46; x = x + 47; x = x + 48; x = x + 49; x = x + 50
        x = x + 51; x = x + 52; x = x + 53; x = x + 54; x = x + 55
        x = x + 56; x = x + 57; x = x + 58; x = x + 59; x = x + 60
        x = x + 61; x = x + 62; x = x + 63; x = x + 64; x = x + 65
        x = x + 66; x = x + 67; x = x + 68; x = x + 69; x = x + 70
        x = x + 71; x = x + 72; x = x + 73; x = x + 74; x = x + 75
        x = x + 76; x = x + 77; x = x + 78; x = x + 79; x = x + 80
        x = x + 81; x = x + 82; x = x + 83; x = x + 84; x = x + 85
        x = x + 86; x = x + 87; x = x + 88; x = x + 89; x = x + 90
        x = x + 91; x = x + 92; x = x + 93; x = x + 94; x = x + 95
        x = x + 96; x = x + 97; x = x + 98; x = x + 99; x = x + 100
        # Build large tuple - pushes 50 items then pops them into a tuple
        big_tuple = (
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
            11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
            21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
            31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
            41, 42, 43, 44, 45, 46, 47, 48, 49, 50,
        )
        return x, len(big_tuple)


# Run Test 3
res_x, res_len = test_if_else_deep(True)
assert res_x == 5050, f"Test 3 True x: expected 5050, got {res_x}"
assert res_len == 1, f"Test 3 True len: expected 1, got {res_len}"

res_x, res_len = test_if_else_deep(False)
assert res_x == 5050, f"Test 3 False x: expected 5050, got {res_x}"
assert res_len == 50, f"Test 3 False len: expected 50, got {res_len}"


print("test_long_jump_extended_arg passed")