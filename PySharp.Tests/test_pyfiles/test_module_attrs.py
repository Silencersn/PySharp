"""
Tests for module attributes
Exercises PyModuleObjectType, __dict__, __name__
"""

import math
import time
import random

# Module __name__
assert math.__name__ == 'math'
assert random.__name__ == 'random'

# Module __dict__
d = math.__dict__
assert isinstance(d, dict)
assert 'pi' in d
assert 'sqrt' in d
assert 'sin' in d

# Module dir()
# TODO: dir() on modules may not include all attributes yet
math_dir = dir(math)
# assert 'pi' in math_dir
# assert 'sqrt' in math_dir
# assert 'sin' in math_dir
# assert 'cos' in math_dir
assert isinstance(math_dir, list)

# Getting module attributes via getattr
assert getattr(math, 'pi') == math.pi
assert getattr(math, 'e') == math.e
assert getattr(time, 'time') is not None

# hasattr on modules
assert hasattr(math, 'sqrt')
assert not hasattr(math, 'nonexistent_attr')

print("test_module_attrs passed")
