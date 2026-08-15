"""
Regression: compile() with an invalid mode must raise ValueError (CPython
raises "compile() mode must be 'exec', 'eval' or 'single'"), not TypeError.

CPython 3.14 reference:
    compile('x = 1', 'f', 'bogus')  -> ValueError: compile() mode must be
                                       'exec', 'eval' or 'single'
    compile('x = 1', 'f', '')       -> ValueError (same)
"""

for mode in ('bogus', '', 'exec ', 'EXEC'):
    try:
        compile('x = 1', 'f', mode)
        assert False, f"compile(mode={mode!r}) should raise ValueError"
    except ValueError:
        pass

# Valid modes still work.
compile('x = 1', 'f', 'exec')
compile('1 + 1', 'f', 'eval')
compile('x = 1', 'f', 'single')

print("test_compile_mode_regression passed")
