"""
Tests for the built-in containers' __class_getitem__ (issue #401).
Tests:
- list/tuple/dict/set/frozenset subscript build types.GenericAlias
- Nested and multi-arg forms, Ellipsis args render bare
- __origin__/__args__, direct __class_getitem__ call
- isinstance/issubclass refuse parameterized generics
- str/bytes/type exclusion and inclusion boundaries

:kind: test
"""
for expr in ("list[int]", "tuple[int]", "dict[int, int]", "set[int]", "frozenset[int]"):
    print(expr, "=", repr(eval(expr)))

print(type(list[int]).__name__, repr(type(list[int])))
print(list[int].__origin__ is list, list[int].__args__)
print(repr(dict[str, tuple[int, ...]]))
print(repr(list.__class_getitem__(int)))

try:
    list.__class_getitem__()
except TypeError as e:
    print("no args:", e)
try:
    list.__class_getitem__(int, str)
except TypeError as e:
    print("two args:", e)

try:
    print(isinstance([1], list[int]))
except TypeError as e:
    print("isinstance:", type(e).__name__, e)
try:
    print(issubclass(list, list[int]))
except TypeError as e:
    print("issubclass:", type(e).__name__, e)

try:
    eval("str[int]")
except TypeError as e:
    print("str:", e)
try:
    eval("bytes[int]")
except TypeError as e:
    print("bytes:", e)
print(repr(type[int]))

x: list[int] = [1, 2]
print(x)
