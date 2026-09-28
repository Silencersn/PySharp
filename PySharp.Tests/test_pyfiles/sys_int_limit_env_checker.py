"""Asserts a fresh environment starts at the default int<->str digit limit.

Driver: TestPyFiles.TestSysIntMaxStrDigitsIsPerEnvironment runs
sys_int_limit_env_setter.py in another environment first; if the limit were
process-global, this fixture's default-limit assertions would fail.

:kind: helper
"""

import sys

assert sys.get_int_max_str_digits() == 4300, sys.get_int_max_str_digits()

v = int("9" * 4300)
assert v % 10 == 9
