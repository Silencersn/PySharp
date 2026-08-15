"""
Regression: input() at EOF (empty stdin) must raise EOFError instead of
returning ''.

CPython 3.14 reference (stdin at EOF):
    input()  # -> EOFError: EOF when reading a line
"""

try:
    input()
    assert False, "input() at EOF should raise EOFError"
except EOFError as e:
    assert str(e) == "EOF when reading a line", f"unexpected message: {e}"

print("test_input_eof_regression passed")
