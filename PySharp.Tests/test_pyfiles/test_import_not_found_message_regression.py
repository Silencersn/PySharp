"""
Regression: a failed import quotes the module name through repr, and the
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

print("test_import_not_found_message_regression passed")
