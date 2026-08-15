"""
Regression: dir() without arguments must return names in sorted order
(CPython semantics), not in insertion order.

CPython 3.14 reference:
    zebra = 1; apple = 2; mango = 3
    dir()
    # ['__builtins__', '__doc__', '__loader__', '__name__', '__package__',
    #  '__spec__', 'apple', 'mango', 'zebra']
"""

zebra = 1
apple = 2
mango = 3

names = dir()
assert names == sorted(names), f"dir() not sorted: {names}"

# The three defined names must be present and appear sorted at the tail
# (all module dunder names sort before the user names).
assert names[-3:] == ['apple', 'mango', 'zebra'], f"unexpected dir(): {names}"

print("test_dir_sorting_regression passed")
