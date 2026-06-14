"""
Tests for template string literals (t-strings, Python 3.14+) - PEP 750
"""
print("testing t-strings")

# Basic t-string
name = "World"
template = t'Hello {name} World'
print("template:", repr(template))

# t-string with multiple interpolations
a, b = 10, 20
t2 = t'{a} + {b} = {a + b}'
print("t2:", repr(t2))

# t-string with expressions
x = 42
t3 = t'The answer is {x}'
print("t3:", repr(t3))

# Empty t-string
t4 = t''
print("t4:", repr(t4))

# t-string with only text
t5 = t'plain text'
print("t5:", repr(t5))

# t-string with only interpolation
t6 = t'{x}'
print("t6:", repr(t6))

# t-string with format spec
val = 3.14159
t7 = t'pi is approximately {val:.2f}'
print("t7:", repr(t7))

print("test_tstring passed")
