"""Verifies the std streams accept close(), expose the closed flag it sets, raise ValueError on further I/O, and treat a second close as a no-op.

:kind: test
:background: `closed` was hardcoded False on the std streams and no code path
    could ever set it, so close() was a no-op and the closed-file ValueError
    guards never fired.
"""

# the std streams must accept close() and expose the closed flag it sets.
# Closing stderr/stdout is checked last so the messages still land somewhere
# while the stream is still open.
import sys

assert sys.stdin.closed is False
assert sys.stdout.closed is False
assert sys.stderr.closed is False

sys.stderr.close()
assert sys.stderr.closed is True
try:
    sys.stderr.write("x")
except ValueError as e:
    assert str(e) == "I/O operation on closed file.", str(e)
else:
    raise AssertionError("expected ValueError")
# a second close is a no-op
sys.stderr.close()
assert sys.stderr.closed is True

sys.stdout.close()
assert sys.stdout.closed is True
try:
    sys.stdout.write("x")
except ValueError as e:
    assert str(e) == "I/O operation on closed file.", str(e)
else:
    raise AssertionError("expected ValueError")

sys.stdin.close()
assert sys.stdin.closed is True
try:
    sys.stdin.read()
except ValueError as e:
    assert str(e) == "I/O operation on closed file.", str(e)
else:
    raise AssertionError("expected ValueError")
