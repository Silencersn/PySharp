"""
Regression: tab/space mixed indentation inconsistency must raise
TabError ("inconsistent use of tabs and spaces in indentation"), the
IndentationError subclass, like CPython (Parser/pegen_errors.c picks the
exception type from the message). PySharp used to raise the plain parent
IndentationError (or even accept the ambiguous form), so `except TabError`
and isinstance checks could never observe it.

Guards pin the boundary: consistent tab indentation stays legal, and a
pure-space indentation mismatch stays a plain IndentationError (not
TabError). TabError.__mro__ is already correct in PySharp.
"""

T1 = "if True:\n\tif True:\n\t\tpass\n        pass\n"
T2 = "if True:\n        if True:\n\t\tpass\n        pass\n"
T3 = "if True:\n\tif True:\n \tpass\n\t\tpass\n"


def expect_tab_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise TabError: " + repr(src)
    except TabError as e:
        assert "inconsistent use of tabs" in str(e), str(e)
    except IndentationError as e:
        assert False, "raised plain IndentationError instead of TabError: " + str(e)


def expect_plain_indentation_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise IndentationError: " + repr(src)
    except TabError:
        assert False, "should be a plain IndentationError, not TabError"
    except IndentationError:
        pass


# red cases: mixed tab/space indentation must raise TabError
expect_tab_error(T1)
expect_tab_error(T2)
expect_tab_error(T3)


# guards
assert issubclass(TabError, IndentationError)

compile("if True:\n\tpass\n", "<test>", "exec")   # consistent tabs stay legal

expect_plain_indentation_error("if True:\n    pass\n  pass\n")

print("test_tab_error_regression passed")
