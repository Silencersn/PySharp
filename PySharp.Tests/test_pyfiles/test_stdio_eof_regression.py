"""
Regression: sys.stdin.readline() at EOF returns '' (not StopIteration),
and sys.stdout/stderr.write() return the number of characters written.
"""
import sys

# readline() at EOF must return '', not raise StopIteration.
assert sys.stdin.readline() == "", "readline() at EOF should return ''"

# write() returns the number of characters written.
assert sys.stdout.write("hello") == 5, "stdout.write() should return char count"
assert sys.stderr.write("err") == 3, "stderr.write() should return char count"

# Standard stream metadata.
assert sys.stdin.name == "<stdin>", f"stdin.name = {sys.stdin.name}"
assert sys.stdout.name == "<stdout>", f"stdout.name = {sys.stdout.name}"
assert sys.stderr.name == "<stderr>", f"stderr.name = {sys.stderr.name}"
assert sys.stdin.readable() is True
assert sys.stdin.writable() is False, "stdin should not be writable"
assert sys.stdout.writable() is True
assert sys.stdout.readable() is False, "stdout should not be readable"
assert sys.stdout.closed is False, "stdout should not be closed"

print("test_stdio_eof_regression passed")
