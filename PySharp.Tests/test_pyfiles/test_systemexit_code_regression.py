# SystemExit.code regression: CPython defines code as a T_OBJECT member
# filled by SystemExit_init - args[0] for one argument, the whole args
# tuple for multiple arguments, None for none; assignment and deletion
# never fall back to args.

e = SystemExit(3)
assert e.code == 3, e.code

e = SystemExit()
assert e.code is None, e.code

e = SystemExit("msg")
assert e.code == "msg", e.code

e = SystemExit(1, 2)
assert e.code == (1, 2), e.code
assert e.args == (1, 2), e.args

e = SystemExit(3)
e.code = 0
assert e.code == 0, e.code
assert e.args == (3,), e.args

e = SystemExit(3)
del e.code
assert e.code is None, e.code

e = SystemExit(3)
assert e.__dict__ == {}, e.__dict__


class MyExit(SystemExit):
    pass


assert MyExit(7).code == 7, MyExit(7).code

try:
    ValueError(1).code
except AttributeError:
    pass
else:
    raise AssertionError("ValueError.code must not exist")

print("ok")
