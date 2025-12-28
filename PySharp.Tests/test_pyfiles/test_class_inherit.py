class Base:
    def greet(self):
        return "hello"

class Derived(Base):
    def greet(self):
        return super().greet() + " world"

d = Derived()
assert d.greet() == "hello world"