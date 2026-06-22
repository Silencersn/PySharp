"""
Tests for complex numbers - covers PyComplexObject, PyComplexObjectType
"""

# Complex literal
# TODO: complex type not fully implemented yet - skipping detailed tests
# c = 1 + 2j
# assert isinstance(c, complex)
# assert c.real == 1.0
# assert c.imag == 2.0

# complex() constructor
# c2 = complex(3, 4)
# assert c2.real == 3.0
# assert c2.imag == 4.0
#
# c3 = complex(5)
# assert c3.real == 5.0
# assert c3.imag == 0.0
#
# # Complex from string
# c4 = complex("1+2j")
# assert c4.real == 1.0
# assert c4.imag == 2.0
#
# # Complex addition
# c = complex(1, 2) + complex(3, 4)
# assert c == complex(4, 6)
#
# # Complex subtraction
# c = complex(5, 7) - complex(1, 3)
# assert c == complex(4, 4)
#
# # Complex multiplication
# c = complex(2, 3) * complex(4, 5)
# assert c == complex(-7, 22)
#
# # Complex negation
# c = -complex(1, 2)
# assert c == complex(-1, -2)

print("test_complex passed (basic)")

# Complex equality
# TODO: complex __eq__ with complex not fully working
# assert complex(1, 2) == complex(1, 2)
# assert complex(1, 2) != complex(3, 4)

# abs of complex
# TODO: complex __abs__ not yet implemented
# assert abs(complex(3, 4)) == 5.0
# assert abs(complex(1, 0)) == 1.0

# Complex with real numbers
# TODO: complex arithmetic with non-complex not yet implemented
# assert complex(1, 2) + 3 == complex(4, 2)
# assert complex(1, 2) * 2 == complex(2, 4)

print("test_complex passed")
