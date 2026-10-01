"""dict() and dict.update() report a fixed sentence for a non-iterable pair element and note the failing index on __notes__.

:kind: test
"""

# Regression: PySequence_Fast receives the caller's fixed sentence, so a
# pair element without iteration support never names its type — unlike
# the outer argument, whose own type still enters the message. The
# failing element's index is attached as a __notes__ entry, and
# BaseException.add_note builds that list on first use.


def show(label, f):
    try:
        r = f()
        print(label, "=", repr(r))
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)), "| notes:", getattr(e, "__notes__", None))


show("update_int_elem", lambda: {"a": 1}.update([1]))
show("update_int_elems", lambda: {"a": 1}.update([1, 2, 3]))
show("update_int_arg", lambda: {"a": 1}.update(1))
show("dict_int_elem", lambda: dict([1]))
show("dict_int_elem2", lambda: dict([('a', 1), 2]))
show("dict_int_arg", lambda: dict(1))
show("dict_none", lambda: dict(None))
show("update_pair_ok", lambda: {"a": 1}.update([("b", 2)]))
show("update_kw", lambda: {"a": 1}.update(a=2))
show("update_mapping", lambda: {"a": 1}.update({"a": 9}))
show("len3", lambda: dict([(1, 2, 3)]))
show("len1", lambda: dict([[1]]))
show("update_len3", lambda: {"a": 1}.update([(1, 2, 3)]))
show("str_elems", lambda: dict(["ab"]))

e2 = ValueError("boom")
print("has_notes_before:", hasattr(e2, "__notes__"))
e2.add_note("first note")
e2.add_note("second note")
print("notes_after:", e2.__notes__)
try:
    e2.add_note(42)
except TypeError as te:
    print("add_note_int!", te)
e3 = ValueError("two")
e3.__notes__ = "not a list"
try:
    e3.add_note("x")
except TypeError as te:
    print("add_note_nonlist!", te)
e4 = ValueError("three")
e4.add_note("kept")
e4.__notes__ = e4.__notes__
print("notes_reassigned:", e4.__notes__)
