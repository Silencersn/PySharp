"""Verifies stdout and stderr never emit a UTF-8 BOM; the C# side asserts the raw captured bytes.

:kind: test
"""

# Regression: stdout/stderr must never emit a UTF-8 BOM (CPython writes
# the preamble only for an explicit utf-8-sig codec). The console host
# hands Console.OutputEncoding - potentially the BOM-emitting UTF8
# singleton - to the stdio writers, which prepended EF BB BF to every
# redirected run. The C# side asserts the raw captured bytes.
import sys

print("hello")
sys.stderr.write("err-line\n")
