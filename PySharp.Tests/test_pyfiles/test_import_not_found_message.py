"""
a failed import quotes the module name through repr, and the
raised ModuleNotFoundError carries the raw name on its `name` attribute.

CPython 3.14 reference: Lib/importlib/_bootstrap.py raises

    raise ModuleNotFoundError(f'{_ERR_MSG_PREFIX}{name!r}', name=name)

so the message is "No module named " + repr(name) — a name containing an
apostrophe switches the message to double quotes, a newline arrives as the
escaped form — and `name` is set from the raw (unescaped) value. PySharp
hardcoded the surrounding single quotes and interpolated the name raw, so
`__import__("a'b")` produced `No module named 'a'b'` and a newline broke the
message across lines; `e.name` was missing entirely.

Objects/exceptions.c ImportError_init is the other half: ImportError (and
hence ModuleNotFoundError) accepts the name/path/name_from keywords, msg is
args[0] for a single argument, and ImportError_str prefers a str msg over the
args rendering.

A dotted name whose parent module imported but has no __path__ fails with an
explicit second clause in the same place (_find_and_load_unlocked):
"No module named '<child>'; '<parent>' is not a package" — the failing
level's child is both the quoted message name and the `name` attribute, so a
grandchild of a plain module reports its parent chain ('p.c', not 'p.c.gc'),
while a missing parent and a missing submodule of a real package stay
note-free.

:kind: test
"""

# --- the plain case is unchanged ---------------------------------------------
try:
    __import__("no_such_module_zz414")
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == "No module named 'no_such_module_zz414'"
    assert e.name == "no_such_module_zz414"

# an import statement goes through the same message
try:
    import no_such_module_zz414_stmt
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == "No module named 'no_such_module_zz414_stmt'"
    assert e.name == "no_such_module_zz414_stmt"

# --- a name containing an apostrophe switches repr to double quotes ----------
try:
    __import__("a'b")
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == 'No module named "a\'b"'
    # the attribute keeps the raw name, not the quoted rendering
    assert e.name == "a'b"

# --- a control character arrives as an escape, not raw ------------------------
try:
    __import__("m\nq")
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == "No module named 'm\\nq'"
    assert "\n" not in str(e)
    assert e.name == "m\nq"

# a name holding both quote kinds is escaped rather than re-quoted
try:
    __import__("it's a \"test\"")
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == "No module named 'it\\'s a \"test\"'"
    assert e.name == "it's a \"test\""

# --- a dotted name whose parent is a plain module carries the note -------------
# CPython _find_and_load_unlocked: the parent imports fine but has no
# __path__, so the child import fails with
#
#     No module named '<child>'; '<parent>' is not a package
#
# where <child> is the failing level's own name — not the originally
# requested one for deeper names
try:
    __import__("import_notpackage_mod.child")
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == ("No module named 'import_notpackage_mod.child'; "
                      "'import_notpackage_mod' is not a package")
    assert e.name == "import_notpackage_mod.child"

# the import statement walks the same machinery
try:
    import import_notpackage_mod.child
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == ("No module named 'import_notpackage_mod.child'; "
                      "'import_notpackage_mod' is not a package")
    assert e.name == "import_notpackage_mod.child"

# importing a grandchild of a plain module names 'parent.child': the error
# belongs to that level's own import, which is where the chain broke
try:
    __import__("import_notpackage_mod.child.grandchild")
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == ("No module named 'import_notpackage_mod.child'; "
                      "'import_notpackage_mod' is not a package")
    assert e.name == "import_notpackage_mod.child"

# a parent that fails its own import is reported without the note
try:
    __import__("no_such_parent_zz445.child")
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert "is not a package" not in str(e)

# a real package with a genuinely missing submodule stays note-free too
try:
    __import__("import_cycle_pkg.missing_zz445")
    assert False, "ModuleNotFoundError expected"
except ModuleNotFoundError as e:
    assert str(e) == "No module named 'import_cycle_pkg.missing_zz445'"
    assert e.name == "import_cycle_pkg.missing_zz445"

# --- ModuleNotFoundError is an ImportError, as always -------------------------
try:
    from no_such_pkg_zz414 import thing
    assert False, "ImportError expected"
except ImportError as e:
    assert isinstance(e, ModuleNotFoundError)
    assert e.name == "no_such_pkg_zz414"

# --- ImportError's own keyword arguments --------------------------------------
e = ImportError("boom", name="nm", path="/p", name_from="nf")
assert str(e) == "boom"
assert e.msg == "boom"
assert e.name == "nm"
assert e.path == "/p"
assert e.name_from == "nf"
assert e.args == ("boom",)

# msg is args[0] only for a single argument
e = ImportError("a", "b")
assert e.msg is None
assert str(e) == "('a', 'b')"

# unset members read back as None, and assignment/deletion are never rejected
e = ImportError("boom")
assert e.name is None
assert e.path is None
assert e.name_from is None
e.name = "z"
assert e.name == "z"
del e.name
assert e.name is None
del e.name
e.msg = "zz"
assert str(e) == "zz"
del e.msg
assert e.msg is None
assert str(e) == "boom"

# an unknown keyword is rejected, naming the type actually raised
try:
    ImportError("boom", foo=1)
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "ImportError() got an unexpected keyword argument 'foo'"

try:
    ModuleNotFoundError("boom", foo=1)
    assert False, "TypeError expected"
except TypeError as e:
    # CPython's format string is hardcoded as "|$OOO:ImportError", so the
    # subclass is still reported under the base name
    assert str(e) == "ImportError() got an unexpected keyword argument 'foo'"

# a str subclass is not an exact str, so ImportError_str falls back to args
class MyStr(str):
    pass


assert str(ImportError(MyStr("boom"))) == "boom"

print("test_import_not_found_message passed")
