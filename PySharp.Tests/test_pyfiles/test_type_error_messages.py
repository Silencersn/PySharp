"""
Regression test: type() argument error messages must match CPython 3.14:
- argument type errors use prefix "type.__new__()"
- wrong argument count uses "type() takes 1 or 3 arguments"
"""


def err_of(fn):
    try:
        fn()
    except TypeError as e:
        return str(e)
    raise AssertionError("expected TypeError")


assert err_of(lambda: type(1, (), {})) == "type.__new__() argument 1 must be str, not int"
assert err_of(lambda: type("x", 1, {})) == "type.__new__() argument 2 must be tuple, not int"
assert err_of(lambda: type("x", (), 1)) == "type.__new__() argument 3 must be dict, not int"
assert err_of(lambda: type("x", ())) == "type() takes 1 or 3 arguments"

print("test_type_error_messages passed")
