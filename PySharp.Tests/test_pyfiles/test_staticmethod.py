class Test:
    @staticmethod
    def add(a, b):
        return a + b

    @staticmethod
    def hello():
        return "Hello, staticmethod!"

def test_staticmethod():
    assert Test.add(1, 2) == 3
    t = Test()
    assert t.add(3, 4) == 7
    assert Test.hello() == "Hello, staticmethod!"
    assert t.hello() == "Hello, staticmethod!"

test_staticmethod()
