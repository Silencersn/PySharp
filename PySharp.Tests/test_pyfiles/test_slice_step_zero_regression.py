"""
Regression: slice with step=0 must raise a catchable ValueError, not leak a
bare .NET exception (issue #19).

CPython 3.14 reference:
    [1,2,3][slice(1,2,0)]  -> ValueError: slice step cannot be zero
    [1,2,3][0:2:0]         -> ValueError (same)
    slice(1,2,0) itself is legal
"""

# constructing a zero-step slice is legal
s = slice(1, 2, 0)
assert s.step == 0


def expect_valueerror(fn):
    try:
        fn()
        assert False, 'should raise ValueError'
    except ValueError as e:
        assert 'slice step cannot be zero' in str(e)


# list
expect_valueerror(lambda: [1, 2, 3][slice(1, 2, 0)])
expect_valueerror(lambda: [1, 2, 3][0:2:0])
expect_valueerror(lambda: [1, 2, 3][::0])

# other sequence types
expect_valueerror(lambda: 'abc'[::0])
expect_valueerror(lambda: (1, 2, 3)[::0])
expect_valueerror(lambda: b'abc'[::0])
expect_valueerror(lambda: bytearray(b'abc')[::0])
expect_valueerror(lambda: range(5)[::0])

print("test_slice_step_zero_regression passed")
