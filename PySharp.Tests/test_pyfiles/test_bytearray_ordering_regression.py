# Regression: bytearray order comparisons (< <= > >=) work against any
# bytes-like operand — bytearray_richcompare accepts every buffer
# protocol object and compares bytewise with a length tiebreak, while
# bytes sends its cross-type comparisons through the reflected slot
# because bytes_richcompare requires two exact bytes operands. bytes
# itself gains the missing <=/>/>= slots so all four directions compare
# by content.

faces = [
    # bytearray x bytearray, all four directions
    ("bytearray(b'ab') < bytearray(b'b')", True),
    ("bytearray(b'b') > bytearray(b'ab')", True),
    ("bytearray(b'ab') <= bytearray(b'ab')", True),
    ("bytearray(b'ab') >= bytearray(b'ab')", True),
    ("bytearray(b'ab') < bytearray(b'ab')", False),
    ("bytearray(b'ab') > bytearray(b'ab')", False),
    # bytearray x bytes and bytes x bytearray
    ("bytearray(b'ab') >= b'aa'", True),
    ("b'aa' >= bytearray(b'ab')", False),
    ("b'ab' <= bytearray(b'ab')", True),
    ("bytearray(b'ab') <= b'ab\\x00'", True),
    ("b'ab' < bytearray(b'ab')", False),
    ("bytearray(b'ab') > b'ab'", False),
    ("b'' < bytearray(b'a')", True),
    ("bytearray(b'a') < b''", False),
    # prefix shorter operand sorts first
    ("b'ab' < bytearray(b'ab\\x00')", True),
    ("bytearray(b'ab\\x00') < b'ab'", False),
    ("bytearray(b'ab') < b'abc'", True),
    ("bytearray(b'abc') > b'ab'", True),
    # bytes x bytes: the four slots compare by content
    ("b'ab' <= b'ab'", True),
    ("b'ab' >= b'ab'", True),
    ("b'b' > b'ab'", True),
    ("b'ab' >= b'b'", False),
    ("b'' <= b''", True),
    ("b'\\x00' < b'\\x01'", True),
    # bytearray accepts memoryview; bytes does not
    ("bytearray(b'ab') < memoryview(b'ac')", True),
    ("memoryview(b'ac') > bytearray(b'ab')", True),
    ("bytearray(b'ab') <= memoryview(b'ab')", True),
    ("b'ab' < memoryview(b'ab')", "TypeError: '<' not supported between instances of 'bytes' and 'memoryview'"),
    ("memoryview(b'ab') < b'ab'", "TypeError: '<' not supported between instances of 'memoryview' and 'bytes'"),
    ("memoryview(b'b') < memoryview(b'a')", "TypeError: '<' not supported between instances of 'memoryview' and 'memoryview'"),
    # unrelated types still raise with the exact message
    ("bytearray(b'ab') < [1, 2]", "TypeError: '<' not supported between instances of 'bytearray' and 'list'"),
    ("b'ab' < 'ab'", "TypeError: '<' not supported between instances of 'bytes' and 'str'"),
]

for expr, expected in faces:
    if isinstance(expected, str):
        try:
            eval(expr)
        except Exception as e:
            assert type(e).__name__ + ": " + str(e) == expected, (expr, type(e).__name__ + ": " + str(e))
        else:
            raise AssertionError(expr)
    else:
        got = eval(expr)
        assert got is expected or got == expected, (expr, got, expected)

# ordering drives sorted/min/max across mixed bytes-like sequences
data = [bytearray(b'b'), b'ab', bytearray(b'ab'), b'a', bytearray(b'')]
assert repr(sorted(data)) == "[bytearray(b''), b'a', b'ab', bytearray(b'ab'), bytearray(b'b')]", repr(sorted(data))
assert repr(min(data)) == "bytearray(b'')", repr(min(data))
assert repr(max(data)) == "bytearray(b'b')", repr(max(data))
assert repr(sorted(data, reverse=True)) == "[bytearray(b'b'), b'ab', bytearray(b'ab'), b'a', bytearray(b'')]", repr(sorted(data, reverse=True))

# LCG sweep: 600 deterministic byte-string pairs over all six operators
state = 0x257257

def rnd(n):
    global state
    state = (state * 6364136223846793005 + 1442695040888963407) & ((1 << 64) - 1)
    return (state >> 33) % n

def make_obj(kind, n):
    data = bytes(rnd(256) for _ in range(n))
    if kind == 0:
        return data
    if kind == 1:
        return bytearray(data)
    return memoryview(data)

ops = ["<", "<=", ">", ">=", "==", "!="]
count = 0
for i in range(600):
    ka = rnd(2) if rnd(4) else rnd(3)
    kb = rnd(2) if rnd(4) else rnd(3)
    a = make_obj(ka, rnd(4))
    b = make_obj(kb, rnd(4))
    op = ops[rnd(6)]
    if ka == 2 or kb == 2:
        if op not in ("==", "!="):
            # memoryview never order-compares against bytes/bytearray
            continue
    else:
        count += 1
    got = eval(f"a {op} b")
    if op == "==":
        assert got is (bytes(a) == bytes(b)), (bytes(a), bytes(b), op, got)
    elif op == "!=":
        assert got is (bytes(a) != bytes(b)), (bytes(a), bytes(b), op, got)
    else:
        ba, bb = bytes(a), bytes(b)
        assert got is (ba < bb if op == "<" else ba <= bb if op == "<=" else ba > bb if op == ">" else ba >= bb), (ba, bb, op, got)

print("ordering faces ok,", count, "sweep cases passed")
print("test_bytearray_ordering_regression passed")
