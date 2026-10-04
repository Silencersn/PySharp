"""Constant folding refuses eager allocation of huge constants.

CPython's constant folder (flowgraph.c) guards each fold against the
result's size: an int/int product is capped at 128 bits, a str/bytes
repeat at 4096 characters, a tuple repeat at 256 items and 1024 total
items including nested tuples, an int power and a left shift at 128
bits. A fold over the cap is refused and the operation stays for
runtime, so compiling "'a' * 10**8" or "2 ** 10**8" is instantaneous
instead of allocating or computing the huge result up front.

'str % x' and 'bytes % x' never fold either (%-formatting may raise),
and runtime results are unchanged on either side of every cap.

CPython 3.14 reference (Python/flowgraph.c const_folding_safe_multiply /
const_folding_safe_power / const_folding_safe_lshift /
const_folding_safe_mod, MAX_INT_SIZE / MAX_COLLECTION_SIZE / MAX_STR_SIZE /
MAX_TOTAL_ITEMS).

:kind: test
"""

# Compiling over-cap expressions must not evaluate them
for src in [
    "'a' * 10**8",
    "'ab' * 10**8",
    "b'a' * 10**8",
    "2 ** 10**8",
    "1 << 10**8",
    "(1,) * 10**8",
    "(1, 2) * 10**8",
]:
    compile(src, "<t>", "eval")

# Refused folds still produce the same runtime values
assert eval(compile("'a' * 5000", "<t>", "eval")) == 'a' * 5000
assert eval(compile("(1, 2) * 300", "<t>", "eval")) == (1, 2) * 300
assert eval(compile("2 ** 10001", "<t>", "eval")) == 2 ** 10001
assert eval(compile("1 << 5000", "<t>", "eval")) == 1 << 5000
assert eval(compile("'a' * -1", "<t>", "eval")) == ''
assert eval(compile("'a' * 0", "<t>", "eval")) == ''

# Empty sequences skip the size guard, but a count too large for an
# index still overflows at runtime (str/tuple repeat converts to ssize_t)
try:
    eval(compile("'' * 10**30", "<t>", "eval"))
    assert False
except OverflowError:
    pass

# str repeat cap: 4096 characters, on either side of it
assert eval(compile("'a' * 4096", "<t>", "eval")) == 'a' * 4096
assert eval(compile("'a' * 4097", "<t>", "eval")) == 'a' * 4097
assert eval(compile("'ab' * 2048", "<t>", "eval")) == 'ab' * 2048
assert eval(compile("'ab' * 2049", "<t>", "eval")) == 'ab' * 2049

# bytes repeat cap: same 4096 bound
assert eval(compile("b'a' * 4096", "<t>", "eval")) == b'a' * 4096
assert eval(compile("b'a' * 4097", "<t>", "eval")) == b'a' * 4097

# tuple repeat caps: 256 items per repeat, 1024 total including nesting
assert eval(compile("(1,) * 256", "<t>", "eval")) == (1,) * 256
assert eval(compile("(1,) * 257", "<t>", "eval")) == (1,) * 257
assert eval(compile("(1, 2) * 128", "<t>", "eval")) == (1, 2) * 128
assert eval(compile("(1, 2) * 129", "<t>", "eval")) == (1, 2) * 129
nested = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)
assert eval(compile("((1,2,3,4,5,6,7,8,9,10),) * 93", "<t>", "eval")) == (nested,) * 93
assert eval(compile("((1,2,3,4,5,6,7,8,9,10),) * 94", "<t>", "eval")) == (nested,) * 94

# int product cap: 128 bits
assert eval(compile("100 * 100", "<t>", "eval")) == 10000
big = int("9" * 150)
assert eval(compile(f"{big} * {big}", "<t>", "eval")) == big * big

# int power cap: bits(base) * exponent stays within 128 bits
assert eval(compile("2 ** 64", "<t>", "eval")) == 2 ** 64
assert eval(compile("2 ** 65", "<t>", "eval")) == 2 ** 65
assert eval(compile("3 ** 63", "<t>", "eval")) == 3 ** 63
assert eval(compile("3 ** 80", "<t>", "eval")) == 3 ** 80
assert eval(compile("10 ** 100", "<t>", "eval")) == 10 ** 100

# zero bases and non-positive exponents have no cap
assert eval(compile("2 ** 0", "<t>", "eval")) == 1
assert eval(compile("2 ** -1", "<t>", "eval")) == 0.5
assert eval(compile("(-2) ** 3", "<t>", "eval")) == -8

# left shift cap: 128 bits; negative shifts raise at runtime
assert eval(compile("1 << 127", "<t>", "eval")) == 1 << 127
assert eval(compile("1 << 128", "<t>", "eval")) == 1 << 128
try:
    eval(compile("1 << -1", "<t>", "eval"))
    assert False
except ValueError:
    pass

# %-formatting never folds: it may raise, so it stays for runtime
try:
    eval(compile("'%d %d' % (1,)", "<t>", "eval"))
    assert False
except TypeError:
    pass

# ordinary folds are unchanged
assert eval(compile("'ab' * 3", "<t>", "eval")) == 'ababab'
assert eval(compile("1 + 2 * 3", "<t>", "eval")) == 7
print("ok")
