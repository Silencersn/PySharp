"""Annotated module used by test_module_annotations.py.

:kind: helper
"""


def _ann():
    print("   [annotation evaluated]")
    return int


z: float = 3.14
w: _ann()

if True:
    taken: bytes
if False:
    skipped: bytes

for _ in range(2):
    loop_site: int

try:
    raise ValueError
except ValueError:
    handler_site: str


def in_scope_checks():
    # PEP 649: the bare name never enters the module globals, so the
    # module scope cannot read it even after import
    print("__annotations__" in globals())
    try:
        __annotations__  # noqa: B018
        print("FAIL: no NameError")
    except NameError:
        print("NameError")
