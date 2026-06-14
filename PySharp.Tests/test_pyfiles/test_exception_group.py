"""
Tests for ExceptionGroup - covers PyBaseExceptionGroupObjectType, PyExceptionGroupObjectType
"""

# Basic ExceptionGroup creation and catching
try:
    raise ExceptionGroup("group error", [ValueError("inner1"), TypeError("inner2")])
except ExceptionGroup as eg:
    assert len(eg.args) >= 2
    assert "group error" in str(eg.args[0])
    assert len(eg.exceptions) == 2

# ExceptionGroup with single exception
try:
    raise ExceptionGroup("single", [ValueError("only one")])
except ExceptionGroup as eg:
    assert len(eg.exceptions) == 1
    assert isinstance(eg.exceptions[0], ValueError)

# ExceptionGroup subclass
class CustomGroup(ExceptionGroup):
    pass

try:
    raise CustomGroup("custom", [ValueError("test")])
except CustomGroup as eg:
    assert isinstance(eg, ExceptionGroup)
    assert isinstance(eg, CustomGroup)

except ExceptionGroup:
    assert False, "Should catch CustomGroup first"

# BaseExceptionGroup
try:
    raise BaseExceptionGroup("base", [ValueError("v"), TypeError("t"), KeyboardInterrupt("k")])
except BaseExceptionGroup as beg:
    assert len(beg.exceptions) == 3

# ExceptionGroup with nested exceptions
try:
    inner_group = ExceptionGroup("inner", [ValueError("deep")])
    raise ExceptionGroup("outer", [inner_group, TypeError("other")])
except ExceptionGroup as eg:
    assert len(eg.exceptions) == 2
    assert isinstance(eg.exceptions[0], ExceptionGroup)
    assert isinstance(eg.exceptions[1], TypeError)

# ExceptionGroup in try/except chain
try:
    try:
        raise ExceptionGroup("nested", [ValueError("v")])
    except* ValueError:
        pass
except ExceptionGroup as eg:
    # some exceptions may remain
    pass

# ExceptionGroup str representation
try:
    raise ExceptionGroup("test message", [ValueError("v")])
except ExceptionGroup as eg:
    s = str(eg)
    assert "test message" in s
    assert "sub-exception" in s

# ExceptionGroup with multiple sub-exceptions in str
try:
    raise ExceptionGroup("multi", [ValueError("a"), TypeError("b")])
except ExceptionGroup as eg:
    s = str(eg)
    assert "sub-exception" in s

# ExceptionGroup derive method returns new group
try:
    raise ExceptionGroup("source", [ValueError("a"), TypeError("b")])
except ExceptionGroup as eg:
    # derive creates a new ExceptionGroup with subset of exceptions
    new_group = eg.derive([eg.exceptions[0]])
    assert new_group is not eg
    assert len(new_group.exceptions) == 1
    assert isinstance(new_group.exceptions[0], ValueError)

# ExceptionGroup derive with all exceptions
try:
    raise ExceptionGroup("all", [ValueError("a")])
except ExceptionGroup as eg:
    new_group = eg.derive(eg.exceptions)
    assert len(new_group.exceptions) == 1

# ExceptionGroup split by type
try:
    raise ExceptionGroup("split_test", [ValueError("v"), TypeError("t"), ValueError("v2")])
except ExceptionGroup as eg:
    # split returns (match_group, rest_group)
    match_eg, rest_eg = eg.split(ValueError)
    assert match_eg is not None
    assert rest_eg is not None
    assert len(match_eg.exceptions) == 2  # two ValueErrors
    assert len(rest_eg.exceptions) == 1   # one TypeError

# ExceptionGroup split - no match
try:
    raise ExceptionGroup("no_match", [TypeError("t")])
except ExceptionGroup as eg:
    match_eg, rest_eg = eg.split(ValueError)
    assert match_eg is None
    assert rest_eg is not None
    assert len(rest_eg.exceptions) == 1

# ExceptionGroup split - all match
try:
    raise ExceptionGroup("all_match", [ValueError("a"), ValueError("b")])
except ExceptionGroup as eg:
    match_eg, rest_eg = eg.split(ValueError)
    assert match_eg is not None
    assert rest_eg is None
    assert len(match_eg.exceptions) == 2

# ExceptionGroup exceptions attribute
try:
    raise ExceptionGroup("attr_test", [ValueError("e1"), TypeError("e2")])
except ExceptionGroup as eg:
    excs = eg.exceptions
    assert isinstance(excs, tuple) or isinstance(excs, list)
    assert len(excs) == 2
    assert isinstance(excs[0], ValueError)
    assert isinstance(excs[1], TypeError)

# ExceptionGroup message attribute
try:
    raise ExceptionGroup("my_message", [ValueError("inner")])
except ExceptionGroup as eg:
    assert eg.message == "my_message"

print("test_exception_group passed")
