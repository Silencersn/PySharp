"""
Regression test: '%c' % surrogate code points leaked a bare
.NET ArgumentOutOfRangeException and crashed constant folding at compile time.

CPython 3.14 semantics (verified):
  - '%c' % 0xD800 -> '\\ud800'  (lone surrogate, valid str)
  - '%c' % 0x10FFFF -> '\\U0010ffff';  '%c' % 0x1F600 -> '\\U0001f600'
  - out-of-range (0x110000, -1, 2**40, 2**63) -> OverflowError
  - 'str % x' is never constant-folded (runs at runtime instead)
"""

# ===== 1. %c with lone surrogates and boundaries =====
# Module-level expressions: must compile and run (no folding of str %).

assert '%c' % 65 == 'A'
assert '%c' % 0xD7FF == '\ud7ff'        # non-surrogate, below range
assert '%c' % 0xD800 == '\ud800'        # lone surrogate
assert '%c' % 0xDFFF == '\udfff'        # lone surrogate
assert '%c' % 0xE000 == '\ue000'        # non-surrogate, above range
assert '%c' % 0x10FFFF == '\U0010ffff'
assert '%c' % 0x1F600 == '\U0001f600'

# ===== 2. %c out of range -> OverflowError (no .NET exception leak) =====

def expect_overflow(fn):
    try:
        fn()
    except OverflowError:
        return
    raise AssertionError("expected OverflowError for %c")


expect_overflow(lambda: '%c' % 0x110000)
expect_overflow(lambda: '%c' % -1)
expect_overflow(lambda: '%c' % 2**40)   # huge int must not overflow .NET int
expect_overflow(lambda: '%c' % 2**63)

# ===== 3. runtime form (variable format, no constant folding) =====

fmt = '%c'
assert fmt % 65 == 'A'
assert fmt % 0xD800 == '\ud800'

print("test_percent_format_regression passed")
