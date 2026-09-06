"""
Regression: a lambda's variable-arguments parameter must not swallow
the body colon as a star annotation - 'lambda *a: a' used to fail
with a bare SyntaxError because ParseParamStarAnnotation treated the
colon like def's star annotation. CPython's ast_for_lambdef shares
the regular arguments grammar and lambdas have no annotations.
"""


def expect(value, expected):
    assert value == expected, (value, expected)


# red cases: the vararg name followed directly by the body colon
f = lambda *a: a
expect(f(1, 2), (1, 2))

g = lambda x, *a: (x, a)
expect(g(1, 2, 3), (1, (2, 3)))

expect((lambda *a: a)(1, 2), (1, 2))

nested = lambda *a: (lambda **k: k)()
expect(nested(1), {})

# controls: neighbouring parameter forms keep parsing
expect((lambda **k: k)(x=1), {'x': 1})
expect((lambda x, *a, **k: (x, a, k))(1, 2, y=3), (1, (2,), {'y': 3}))
expect((lambda x, /, y: (x, y))(1, 2), (1, 2))
expect((lambda x, *, y=2: (x, y))(1), (1, 2))
expect((lambda *a, y=1: (a, y))(8), ((8,), 1))
expect((lambda a=1, *args, b, **kw: (a, args, b, kw))(9, 0, b=5, z=6),
       (9, (0,), 5, {'z': 6}))

# guard: def keeps its star annotations
def d(*a: int):
    return a


expect(d(4), (4,))

print("test_lambda_star_regression passed")
