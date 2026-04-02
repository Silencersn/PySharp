"""
Class inheritance and super() behavior tests
"""

class Base:
    def __init__(self, name):
        self.name = name

    def greet(self):
        return f"hello, {self.name}"

class Derived(Base):
    def __init__(self, name, title):
        super().__init__(name)
        self.title = title

    def greet(self):
        return f"{self.title}: {super().greet()} world"

# Test simple inheritance
d = Derived("python", "mr")
assert d.name == "python"
assert d.title == "mr"
assert d.greet() == "mr: hello, python world"

# Test is-a relationships
assert isinstance(d, Derived)
assert isinstance(d, Base)
assert issubclass(Derived, Base)

class GrandChild(Derived):
    def greet(self):
        return "GrandChild: " + super().greet()

gc = GrandChild("child", "dr")
assert gc.name == "child"
assert gc.greet() == "GrandChild: dr: hello, child world"

print("test_class_inherit passed")
