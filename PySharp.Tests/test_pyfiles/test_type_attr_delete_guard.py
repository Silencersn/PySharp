"""Deleting __name__/__qualname__/__module__/__bases__/__doc__ on a type raises TypeError with CPython's immutable-type wording even on heap types, while plain attributes still delete, renaming works, and __annotations__ follows its lazy-materialization delete face.

:kind: test
"""

# Deleting type-object attributes: __name__/__qualname__/__module__/
# __bases__/__doc__ have no deleter in CPython — the delete path raises
# TypeError "cannot delete ... attribute of immutable type ..." even on
# heap types, carrying the bare type name. Immutable builtin types report
# the "cannot set ..." wording for the same delete. __annotations__ raises
# a bare-name AttributeError until the lazy getter materializes the dict.

def expect(label, fn, exc, msg):
    try:
        fn()
    except exc as e:
        assert str(e) == msg, f"{label}: {str(e)!r} != {msg!r}"
    else:
        assert False, f"{label}: no {exc.__name__}"


class A:
    x = 1


def _del_name():
    del A.__name__
expect("del A.__name__", _del_name, TypeError,
       "cannot delete '__name__' attribute of immutable type 'A'")


def _del_qualname():
    del A.__qualname__
expect("del A.__qualname__", _del_qualname, TypeError,
       "cannot delete '__qualname__' attribute of immutable type 'A'")


def _del_module():
    del A.__module__
expect("del A.__module__", _del_module, TypeError,
       "cannot delete '__module__' attribute of immutable type 'A'")


def _del_bases():
    del A.__bases__
expect("del A.__bases__", _del_bases, TypeError,
       "cannot delete '__bases__' attribute of immutable type 'A'")


def _del_doc():
    del A.__doc__
expect("del A.__doc__", _del_doc, TypeError,
       "cannot delete '__doc__' attribute of immutable type 'A'")

# the failed deletes leave the metadata intact
assert A.__name__ == "A"
assert A.__qualname__ == "A"
assert A.__doc__ is None


# a docstring does not change the refusal
class WithDoc:
    "doc"


def _del_doc2():
    del WithDoc.__doc__
expect("del WithDoc.__doc__", _del_doc2, TypeError,
       "cannot delete '__doc__' attribute of immutable type 'WithDoc'")
assert WithDoc.__doc__ == "doc"


# nested classes report the bare name
class Outer:
    class Inner:
        y = 2


def _del_inner_name():
    del Outer.Inner.__name__
expect("del Outer.Inner.__name__", _del_inner_name, TypeError,
       "cannot delete '__name__' attribute of immutable type 'Inner'")


def _del_inner_qualname():
    del Outer.Inner.__qualname__
expect("del Outer.Inner.__qualname__", _del_inner_qualname, TypeError,
       "cannot delete '__qualname__' attribute of immutable type 'Inner'")


# type()-created classes are heap types and guarded alike
T = type("B", (), {})


def _del_t_name():
    del T.__name__
expect("del T.__name__", _del_t_name, TypeError,
       "cannot delete '__name__' attribute of immutable type 'B'")


# plain class attributes still delete
def _del_x():
    del A.x
_del_x()
assert not hasattr(A, "x")
assert "x" not in vars(A)


def _del_missing():
    del A.zzz
expect("del A.zzz", _del_missing, AttributeError,
       "type object 'A' has no attribute 'zzz'")


# immutable builtin types report the set-message on the delete face
def _del_int_name():
    del int.__name__
expect("del int.__name__", _del_int_name, TypeError,
       "cannot set '__name__' attribute of immutable type 'int'")


def _del_int_doc():
    del int.__doc__
expect("del int.__doc__", _del_int_doc, TypeError,
       "cannot set '__doc__' attribute of immutable type 'int'")


# non-str setters use CPython's wording with the bare type name
def _set_name_int():
    A.__name__ = 5
expect("A.__name__ = 5", _set_name_int, TypeError,
       "can only assign string to A.__name__, not 'int'")


def _set_qualname_int():
    A.__qualname__ = 5
expect("A.__qualname__ = 5", _set_qualname_int, TypeError,
       "can only assign string to A.__qualname__, not 'int'")


def _set_inner_qualname_int():
    Outer.Inner.__qualname__ = 5
expect("Outer.Inner.__qualname__ = 5", _set_inner_qualname_int, TypeError,
       "can only assign string to Inner.__qualname__, not 'int'")


# renaming still works
A.__name__ = "Renamed"
assert A.__name__ == "Renamed"
A.__qualname__ = "Renamed.Q"
assert A.__qualname__ == "Renamed.Q"
A.__name__ = "A"
A.__qualname__ = "A"


# __annotations__: deleting before the lazy getter materialized the dict
# raises a bare-name AttributeError; after access it deletes and the next
# read recreates an empty dict
class B:
    pass


def _del_ann_fresh():
    del B.__annotations__
expect("del B.__annotations__", _del_ann_fresh, AttributeError,
       "__annotations__")

B.__annotations__


def _del_ann():
    del B.__annotations__
_del_ann()
assert B.__annotations__ == {}

print("test_type_attr_delete_guard passed")
