class MyClass:
    def __init__(self, value):
        self.value = value

    def get_value(self):
        return self.value

obj = MyClass(42)
assert obj.get_value() == 42