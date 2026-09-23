"""
Import cycle regression

A module body used to run before its module object was registered, so an
import that resolved back to the module being initialized reentered the body
until RecursionError. CPython registers the module first and runs the body
afterwards (importlib._bootstrap._load_unlocked), which is what makes the four
shapes below work.
"""

# 1. A package importing its own submodule from __init__ (the reported shape).
import import_cycle_pkg

assert import_cycle_pkg.VALUE == 42
assert import_cycle_pkg.sub is not None
assert import_cycle_pkg.B == "b"
assert import_cycle_pkg.HELPER_FROM_INIT == "helper:42"
# The submodule read the package while the package was still initializing.
assert import_cycle_pkg.sub.HELPER == "helper:42"

# 2. Plain modules importing each other: every body runs once, and the second
#    one finds the first one only half built.
import import_cycle_moda
import import_cycle_modb

assert import_cycle_moda.A_VAL == "a"
assert import_cycle_moda.SAW_B == "b"
assert import_cycle_modb.B_VAL == "b"
assert import_cycle_modb.SAW_A is False

# 3. A body that raises leaves no registration behind, so the next import runs
#    it again instead of handing out a half-initialized module.
import import_cycle_counter

failures = 0
for _ in range(2):
    try:
        import import_cycle_fail
    except ValueError:
        failures += 1

assert failures == 2
assert import_cycle_counter.COUNT == 2

# 4. A circular from-import that cannot be satisfied raises ImportError; it used
#    to recurse into RecursionError instead. Only the message prefix is pinned:
#    PySharp does not append CPython's location/suggestion suffix yet.
try:
    import import_cycle_p
except ImportError as error:
    assert "cannot import name 'P_VAL'" in str(error), error
else:
    raise AssertionError("an unsatisfiable circular from-import must raise ImportError")

print("All import cycle tests passed!")
