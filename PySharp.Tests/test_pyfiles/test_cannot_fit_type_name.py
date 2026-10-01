"""cannot fit '...' into an index-sized integer names the operand's type.

:kind: test
"""

# Regression: the sentence hardcoded 'int'. CPython's PyNumber_AsSsize_t
# names the object that produced the out-of-range value — a custom
# __index__ class or an int subclass — except where the value was already
# a plain int when it reached the narrowing (a __len__ result, a literal
# 2**70), which correctly reports 'int'.

class BigIdx:
    def __index__(self): return 2 ** 70

class MyInt(int): pass

class NegIdx:
    def __index__(self): return -2 ** 70

class BigLen:
    def __len__(self): return 2 ** 70

def try_op(label, fn):
    try:
        print(label, "=", repr(fn()))
    except Exception as e:
        print(label, "!", type(e).__name__ + ":", e)

try_op("list-get", lambda: [1][BigIdx()])
try_op("tuple-get", lambda: (1,)[BigIdx()])
try_op("list-set", lambda: exec("x = [1]\nx[BigIdx()] = 2", {"BigIdx": BigIdx}))
try_op("list-get-MyInt", lambda: [1][MyInt(2 ** 70)])
try_op("list-get-Neg", lambda: [1][NegIdx()])
try_op("list-repeat", lambda: [1] * BigIdx())
try_op("tuple-repeat", lambda: (1,) * BigIdx())
try_op("str-repeat", lambda: "a" * BigIdx())
try_op("list-repeat-int", lambda: [1] * (2 ** 70))
try_op("bytes-get", lambda: b"ab"[BigIdx()])
try_op("bytearray-get", lambda: bytearray(b"ab")[BigIdx()])
try_op("bytes-repeat", lambda: b"a" * BigIdx())
try_op("bytearray-repeat", lambda: bytearray(b"a") * BigIdx())
try_op("len-big", lambda: len(BigLen()))
try_op("range-get", lambda: range(1)[BigIdx()])
try_op("list-in-range", lambda: [1][MyInt(2**70)])

path = "pys472_tmp_data.txt"
with open(path, "w") as g:
    g.write("hello\nworld\n")
for mode in ("r", "rb"):
    def mkread(mode=mode):
        def run():
            f = open(path, mode)
            try:
                return f.read(BigIdx())
            finally:
                f.close()
        return run
    def mkline(mode=mode):
        def run():
            f = open(path, mode)
            try:
                return f.readline(BigIdx())
            finally:
                f.close()
        return run
    def mklines(mode=mode):
        def run():
            f = open(path, mode)
            try:
                return f.readlines(BigIdx())
            finally:
                f.close()
        return run
    try_op(mode + "-read", mkread())
    try_op(mode + "-readline", mkline())
    try_op(mode + "-readlines", mklines())
