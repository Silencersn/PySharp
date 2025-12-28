class Special:
    def __init__(self, value):
        self.value = value

    def __str__(self):
        return f"Special.__str__({self.value})"

    def __repr__(self):
        return f"Special.__repr__({self.value})"

    def __eq__(self, other):
        if isinstance(other, Special):
            return self.value == other.value
        return False

    def __add__(self, other):
        if isinstance(other, Special):
            return Special(self.value + other.value)
        return NotImplemented

    def __len__(self):
        return self.value

a = Special(3)
b = Special(4)

assert str(a) == "Special.__str__(3)"
assert repr(a) == "Special.__repr__(3)"
assert a == Special(3)
assert a != b
c = a + b
assert isinstance(c, Special)
assert c.value == 7
assert len(a) == 3