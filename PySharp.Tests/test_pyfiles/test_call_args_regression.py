"""
Call-args regression tests.

Cover paths introduced by b440261 (Span/ArrayPool argument parsing) that the
general test suite does not exercise (per PyArgsDef instrumentation):
  - BufferLength > 8: ArrayPool buffer path (0% coverage before this file)
  - kwonly with / without kwargs
  - posonly + kwargs mix
  - duplicate positional/kwarg conflict (new TryParseGeneral check)
  - calling positional parameters via keyword
"""

# ===== 1. BufferLength > 8: ArrayPool buffer path =====

# 9 params (8 required + 1 default); call site passes only 8 positional args
# -> BufferLength = 9 > 8
def f9(a, b, c, d, e, f_, g_, h_, i=0):
    return a + b + c + d + e + f_ + g_ + h_ + i

assert f9(1, 2, 3, 4, 5, 6, 7, 8) == 36
assert f9(1, 2, 3, 4, 5, 6, 7, 8, 9) == 45

# 12 params (8 required + 4 default) -> BufferLength = 12 > 8
def f12(a, b, c, d, e, f_, g_, h_, i=0, j=0, k=0, l=0):
    return a + b + c + d + e + f_ + g_ + h_ + i + j + k + l

assert f12(1, 2, 3, 4, 5, 6, 7, 8) == 36

# All 12 params passed as keyword args (kwargs -> positional fill + ArrayPool buffer)
assert f12(a=1, b=2, c=3, d=4, e=5, f_=6, g_=7, h_=8, i=9, j=10, k=11, l=12) == 78
# Mixed positional + keyword
assert f12(1, 2, 3, 4, 5, 6, 7, 8, l=12) == 48

# ===== 2. kwonly without kwargs (KwDefaults.CopyTo path) =====
def k2(a, b, *, c=0, d=0):
    return a + b + c + d

assert k2(1, 2) == 3                 # kwonly all use defaults
assert k2(1, 2, c=5) == 8            # partial kwonly via keyword
assert k2(1, 2, c=5, d=6) == 14      # all kwonly via keyword

# kwonly without a default must be passed explicitly
def kreq(a, *, x):
    return a + x

assert kreq(1, x=2) == 3
try:
    kreq(1)
    assert False, "kwonly without default must be passed as keyword"
except TypeError:
    pass

# ===== 3. Positional params passed as keyword + duplicate conflict =====
def dup(a, b):
    return a + b

assert dup(1, b=2) == 3              # b passed as keyword (slot not filled positionally)
assert dup(a=1, b=2) == 3            # all passed as keyword
try:
    dup(1, a=2)                      # a already filled positionally -> TypeError
    assert False, "duplicate argument should raise TypeError"
except TypeError:
    pass

# ===== 4. posonly: positional-only params =====
def po(a, b, /, c=0, d=0):
    return a + b + c + d

assert po(1, 2) == 3
assert po(1, 2, 3) == 6
assert po(1, 2, c=3, d=4) == 10      # keywords allowed after posonly
try:
    po(a=1, b=2, c=3)                # posonly cannot be passed as keyword
    assert False, "posonly params cannot be passed as keyword"
except TypeError:
    pass

# ===== 5. *args / **kwargs combination =====
def mixed(a, b=0, *args, c=0, **kw):
    return a + b + sum(args) + c + kw.get('x', 0)

assert mixed(1) == 1
assert mixed(1, 2, 3, 4, c=5, x=6) == 21
assert mixed(1, c=5, x=6) == 12

# Unknown keyword without **kwargs -> TypeError
def nokw(a):
    return a

try:
    nokw(1, zzz=2)
    assert False, "unknown keyword should raise TypeError"
except TypeError:
    pass
