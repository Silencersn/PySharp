# test_builtin_maxmin_print:
#   max()/min() key parameter and default semantics
#   print() file/flush parameters

# === max/min: key parameter ===
assert max([1, 2, 3], key=lambda x: -x) == 1
assert min([1, 2, 3], key=lambda x: -x) == 3
assert max(['a', 'bbb', 'cc'], key=len) == 'bbb'
assert min(['a', 'bbb', 'cc'], key=len) == 'a'

# Multiple positional arguments
assert max(1, 2, 3, key=lambda x: -x) == 1
assert min(1, 2, 3, key=lambda x: -x) == 3
assert max(1, 5, 3, key=lambda x: x % 3) == 5
assert min(1, 5, 3, key=lambda x: x % 3) == 3

# key=None falls back to direct comparison
assert max([1, 5, 3], key=None) == 5
assert min([1, 5, 3], key=None) == 1

# Stable: when several elements share the same key, the first one wins
assert max([1, 3, 5], key=lambda x: x % 2) == 1
assert min([1, 3, 5], key=lambda x: x % 2) == 1

# default combined with key: empty iterable returns default
assert max([], key=len, default=42) == 42
assert min([], key=len, default=42) == 42

# === max/min: default is not compared (regression fix) ===
assert max([1, 2, 3], default=100) == 3
assert min([1, 2, 3], default=100) == 1
assert max([], default=100) == 100
assert min([], default=100) == 100

# === max/min: error propagation ===
try:
    max([1, 2, 3], key=lambda x: 1 / 0)
    assert False, "key error should propagate"
except ZeroDivisionError:
    pass

try:
    min([1, 2, 3], key=5)
    assert False, "non-callable key should raise TypeError"
except TypeError:
    pass

# === print: file / flush ===
class Collector:
    def __init__(self):
        self.parts = []
        self.flush_count = 0
    def write(self, s):
        self.parts.append(s)
    def flush(self):
        self.flush_count += 1

# file parameter: objects, sep and end are written via individual write calls
c = Collector()
print('a', 'b', sep='-', end='!', file=c)
assert c.parts == ['a', '-', 'b', '!']
assert c.flush_count == 0

# flush=True invokes the flush method
c = Collector()
print('x', file=c, flush=True)
assert c.parts == ['x', '\n']
assert c.flush_count == 1

# With no objects, only end is written
c = Collector()
print(file=c)
assert c.parts == ['\n']

# sep/end of None falls back to the defaults
c = Collector()
print('a', 'b', sep=None, end=None, file=c)
assert c.parts == ['a', ' ', 'b', '\n']

# file without a write attribute raises AttributeError
try:
    print('x', file=123)
    assert False, "print(file=123) should raise AttributeError"
except AttributeError:
    pass

# Write to a real file
with open("_test_print_out.txt", "w") as f:
    print('hello', 'world', sep='-', end='!', file=f)
with open("_test_print_out.txt", "r") as f:
    content = f.read()
assert content == 'hello-world!'

# Write to a real file with flush
with open("_test_print_out2.txt", "w") as f:
    print('data', file=f, flush=True)
with open("_test_print_out2.txt", "r") as f:
    assert f.read() == 'data\n'
