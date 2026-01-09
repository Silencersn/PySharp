class Test:
    value = 0

    @classmethod
    def set_value(cls, v):
        cls.value = v
        return cls.value

    @classmethod
    def get_value(cls):
        return cls.value

def test_classmethod():
    assert Test.set_value(10) == 10
    assert Test.get_value() == 10
    t = Test()
    assert t.set_value(20) == 20
    assert t.get_value() == 20
    assert Test.value == 20

test_classmethod()
