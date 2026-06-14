"""
Regression test: sorted() with custom objects
Previously crashed with: Context is not initialized or is disposed.
"""
class Item:
    def __init__(self, value):
        self.value = value
    def __lt__(self, other):
        return self.value < other.value
    def __eq__(self, other):
        if not isinstance(other, Item):
            return False
        return self.value == other.value
    def __repr__(self):
        return f'Item({self.value})'

items = [Item(5), Item(2), Item(8), Item(1)]
sorted_items = sorted(items)
assert sorted_items[0].value == 1
assert sorted_items[1].value == 2
assert sorted_items[2].value == 5
assert sorted_items[3].value == 8

# Reverse sort
sorted_items = sorted(items, reverse=True)
assert sorted_items[0].value == 8
assert sorted_items[1].value == 5
assert sorted_items[2].value == 2
assert sorted_items[3].value == 1

# Sort with key
class NamedItem:
    def __init__(self, name, value):
        self.name = name
        self.value = value
    def __repr__(self):
        return f'NamedItem({self.name}, {self.value})'

named_items = [NamedItem('b', 2), NamedItem('a', 5), NamedItem('c', 1)]
sorted_by_name = sorted(named_items, key=lambda x: x.name)
assert sorted_by_name[0].name == 'a'
assert sorted_by_name[1].name == 'b'
assert sorted_by_name[2].name == 'c'

sorted_by_val = sorted(named_items, key=lambda x: x.value)
assert sorted_by_val[0].value == 1
assert sorted_by_val[1].value == 2
assert sorted_by_val[2].value == 5

print("test_regression_sorting passed")
