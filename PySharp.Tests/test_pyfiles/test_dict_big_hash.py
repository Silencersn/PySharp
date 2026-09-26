"""
a dict key's hash only feeds the probe (a uint bucket
index), so the hash must not be forced through the throwing Int32Value
conversion - any hash outside the int32 range used to raise a bare
OverflowError on every key operation. CPython's lookdict hashes
through PyObject_Hash with arbitrary precision.

:kind: test
"""


def expect(value, expected):
    assert value == expected, (value, expected)


# red cases: hashes just past the int32 range, both sides
expect({2**31: 'a'}[2**31], 'a')
expect({-(2**31) - 1: 'a'}[-(2**31) - 1], 'a')
expect({10**18: 'a'}[10**18], 'a')
expect({10**30: 'a'}[10**30], 'a')

# int/float keys of equal value and equal hash cross-lookup
d = {}
d[10**10] = 'x'
expect(d[float(10**10)], 'x')

d = {float(10**10): 'x'}
expect(d[10**10], 'x')

# non-integral and wide float keys store and find like CPython
d = {}
d[1.5] = 'f'
expect(d[1.5], 'f')
expect({0.5: 'x'}[0.5], 'x')
expect({1e100: 'x'}[1e100], 'x')

# every entry point shares the same lookup path
expect(2**31 in {2**31: 'a'}, True)
expect({2**31: 'a'}.get(2**31), 'a')
expect({2**31: 'a'}.setdefault(2**31, 'z'), 'a')
expect({2**31: 'a'}.pop(2**31), 'a')
d = {2**31: 'a'}
del d[2**31]
expect(d, {})

# controls: the last in-range hashes keep working
expect({2**31 - 1: 'a'}[2**31 - 1], 'a')
expect({-(2**31): 'a'}[-(2**31)], 'a')
expect({0: 'zero'}[0.0], 'zero')
d = {1: 'a'}
d[1.0] = 'b'
expect(d, {1: 'b'})

print("test_dict_big_hash passed")
