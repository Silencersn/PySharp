"""
Regression: open() must reject modes that contain none of r/w/a/x (CPython
raises ValueError instead of silently opening the file in read mode).

CPython 3.14 reference (all ValueError: "Must have exactly one of
create/read/write/append mode and at most one plus"):
    open(path, '')    -> ValueError
    open(path, 'b')   -> ValueError
    open(path, 't')   -> ValueError
    open(path, '+')   -> ValueError
    open(path, 'b+')  -> ValueError
"""

# Pre-create the probe file so that a silently-opened empty mode would succeed
# (exposing the bug) instead of raising FileNotFoundError.
with open("_test_open_mode_probe.txt", "w") as f:
    f.write("probe")

for mode in ('', 'b', 't', '+', 'b+'):
    try:
        f = open("_test_open_mode_probe.txt", mode)
        f.close()
        assert False, f"open(mode={mode!r}) should raise ValueError"
    except ValueError:
        pass

# Valid modes must still work.
with open("_test_open_mode_probe.txt", "w") as f:
    f.write("x")
with open("_test_open_mode_probe.txt", "rb") as f:
    assert f.read() == b"x"

print("test_open_mode_regression passed")
