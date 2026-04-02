class Test:
    value = 0

    @classmethod
    def set_value(cls, v):
        cls.value = v
        return cls.value

    @classmethod
    def get_value(cls):
        return cls.value

    @classmethod
    def get_cls(cls):
        return cls

def test_classmethod():
    # Basic class calls
    assert Test.set_value(10) == 10
    assert Test.get_value() == 10
    assert Test.get_cls() is Test

    # Instance calls
    t = Test()
    assert t.set_value(20) == 20
    assert t.get_value() == 20
    assert Test.value == 20
    assert t.get_cls() is Test

    # Inheritance behavior
    class SubTest(Test):
        pass

    assert SubTest.get_cls() is SubTest
    assert SubTest.set_value(30) == 30
    assert SubTest.value == 30
    # Note: Test.value is still 20, set_value(cls, v) sets on cls
    assert Test.value == 20

    # Error cases - calling with wrong argument count
    try:
        Test.set_value()
        assert False, "Should raise TypeError for missing argument"
    except TypeError:
        pass

    try:
        Test.set_value(1, 2)
        assert False, "Should raise TypeError for extra argument"
    except TypeError:
        pass

    try:
        t.set_value(1, 2)
        assert False, "Should raise TypeError for extra argument (instance call)"
    except TypeError:
        pass

test_classmethod()
print("test_classmethod passed")
