"""
Variable deletion and closure variable tests
Covers: DeleteName, DeleteFast, DeleteGlobal, DeleteDeref, _DeleteDerefFast,
        StoreDeref, _StoreDerefIncludedNonInlineFrame
"""

# --- DeleteName: del at module level ---
x_mod = 42
del x_mod
assert 'x_mod' not in dir()

# --- DeleteGlobal: del with global inside function ---
y_global = 100

def test_delete_global():
    global y_global
    del y_global

test_delete_global()
assert 'y_global' not in dir()

# --- DeleteFast and StoreDeref: del local and nonlocal inside nested function ---
def test_nested_scope():
    a = 10
    b = 20
    c = 30

    def inner():
        nonlocal a, b, c
        # StoreDeref: assign to nonlocal
        a = 100
        b = 200
        # DeleteFast: del local variable
        local_val = 999
        del local_val
        # DeleteDeref and _DeleteDerefFast: del nonlocal variables
        del a
        del b
        # test that 'a' is gone but 'c' remains
        assert c == 30
        try:
            _ = a
            assert False, "a should be deleted"
        except NameError:
            pass
        try:
            _ = b
            assert False, "b should be deleted"
        except NameError:
            pass

    inner()
    # Verify outer variables are also deleted
    try:
        _ = a
        assert False, "outer a should be deleted"
    except NameError:
        pass
    try:
        _ = b
        assert False, "outer b should be deleted"
    except NameError:
        pass
    assert c == 30

test_nested_scope()

# --- DeleteDeref variant: deleting nonlocal in deeper nesting ---
def test_deep_nested_del():
    outer_var = 1
    middle_var = 2

    def middle():
        nonlocal outer_var, middle_var
        inner_var = 3

        def innermost():
            nonlocal outer_var, middle_var
            # DeleteDeref on multiple nonlocal vars
            del outer_var
            del middle_var
            # DeleteFast on inner local
            tmp = 999
            del tmp

        innermost()

        # middle_var should be deleted now
        try:
            _ = middle_var
            assert False, "middle_var should be deleted"
        except NameError:
            pass
        try:
            _ = outer_var
            assert False, "outer_var should be deleted"
        except NameError:
            pass

    middle()

test_deep_nested_del()

# --- _StoreDerefIncludedNonInlineFrame: walrus in comprehension targeting closure var ---
def test_walrus_in_comprehension_closure():
    acc = 0

    def inner():
        nonlocal acc
        # Walrus operator in comprehension
        # The walrus target 'acc' is a cell var, should emit _StoreDerefIncludedNonInlineFrame
        result = [acc := i for i in range(3)]
        return result, acc

    result, final_acc = inner()
    assert result == [0, 1, 2]
    assert final_acc == 2

test_walrus_in_comprehension_closure()

# --- StoreDeref: assign to nonlocal through multiple nesting levels ---
def test_nested_nonlocal_assign():
    value = 0

    def level1():
        nonlocal value
        value = 10

        def level2():
            nonlocal value
            value = 20

        level2()
        assert value == 20

    level1()
    assert value == 20

test_nested_nonlocal_assign()

# --- DeleteName via except handler cleanup ---
def test_except_name_cleanup():
    try:
        raise ValueError("test")
    except ValueError as e:
        msg = str(e)
        assert msg == "test"
    # After the except block, 'e' should be deleted (DeleteName)
    try:
        _ = e
        assert False, "e should be deleted after except block"
    except NameError:
        pass

test_except_name_cleanup()

# --- DeleteFast in function ---
def test_delete_fast_local():
    local_a = 1
    local_b = 2
    del local_a
    try:
        _ = local_a
        assert False
    except NameError:
        pass
    assert local_b == 2

test_delete_fast_local()

# --- DeleteName via class body local deletion ---
class TestClassBody:
    # Define class-level variable
    class_var = 42
    # Delete name from class scope (triggers DeleteName at class level)
    del class_var

# Verify class_var was deleted from class namespace
assert not hasattr(TestClassBody, 'class_var')

# --- Additional class body deletion with nonlocal-like pattern ---
class_var2 = 100
class TestClassBody2:
    # This reads outer scope and creates local
    local_x = class_var2
    del local_x  # DeleteName on class local

assert not hasattr(TestClassBody2, 'local_x')
# The outer class_var2 should still exist
assert class_var2 == 100

# --- Exception handler name deletion (DeleteName in runtime) ---
def test_except_name_deleted():
    try:
        raise RuntimeError("cleanup_test")
    except RuntimeError as err:
        msg = str(err)
        assert msg == "cleanup_test"
    # 'err' should be deleted after except block
    try:
        _ = err
        assert False, "err should be undefined after except"
    except NameError:
        pass

test_except_name_deleted()

print("test_del_variable passed")
