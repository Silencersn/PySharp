"""
sys.argv tests
"""

import sys

assert isinstance(sys.argv, list), f"sys.argv should be a list, got {type(sys.argv)}"
assert len(sys.argv) == 3, f"expected 3 argv entries, got {len(sys.argv)}: {sys.argv}"
assert sys.argv[0].endswith("test_sys_argv.py"), f"argv[0] = {sys.argv[0]}"
assert sys.argv[1] == "alpha"
assert sys.argv[2] == "beta"

print("test_sys_argv passed")
