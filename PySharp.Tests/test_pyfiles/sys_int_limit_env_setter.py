"""Lowers this environment's int<->str digit limit to 640.

Driver: TestPyFiles.TestSysIntMaxStrDigitsIsPerEnvironment runs this fixture
in one environment and sys_int_limit_env_checker.py in a fresh one; with
per-environment limit state the checker must still see the default.

:kind: helper
"""

import sys

sys.set_int_max_str_digits(640)
assert sys.get_int_max_str_digits() == 640
