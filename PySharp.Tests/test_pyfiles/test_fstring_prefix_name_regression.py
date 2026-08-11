"""
Regression: single-letter f/t function names must not be mistaken for
f-string/t-string prefixes.

CPython 3.14 reference: f/t/F/T are ordinary identifiers, so calling such a
function with a string argument compiles and runs normally:

    def f(x): return x
    f('a')      -> 'a'

Previously the lexer treated any text starting with f/F/t/T followed by a
quote within the first 3 characters as an f-string/t-string prefix, so
calls like f('a') / t('a') / F('a') / T('a') (including inside expressions
such as list comprehensions) were mislexed and raised
"SyntaxError: unmatched ')'".

Names starting with other prefix letters (b/B/r/R/u/U) or longer names
(foo, t2, tr) are unaffected; real f-string / t-string literals must still
lex correctly.
"""

def f(x): return x
def t(x): return x
def F(x): return x
def T(x): return x
def g(x): return x
def b(x): return x
def r(x): return x
def u(x): return x
def foo(x): return x
def t2(x): return x
def h(): return 'noargs'

# --- single-letter f/t calls with a string argument ---
assert f('a') == 'a'
assert t('a') == 'a'
assert F('a') == 'a'
assert T('a') == 'a'
assert f("d") == 'd'
assert t("d") == 'd'
assert F("d") == 'd'
assert T("d") == 'd'

# --- names starting with other prefix letters ---
assert g('a') == 'a'
assert b('a') == 'a'
assert r('a') == 'a'
assert u('a') == 'a'

# --- longer names / keyword argument ---
assert foo('a') == 'a'
assert t2('a') == 'a'
assert foo(x='k') == 'k'

# --- no-argument call ---
assert h() == 'noargs'

# --- calls inside expressions / comprehensions ---
assert [f('a') for _ in range(2)] == ['a', 'a']
assert [t('a') for _ in range(2)] == ['a', 'a']
assert (f('a'), t('a'), F('a'), T('a')) == ('a', 'a', 'a', 'a')
assert sum(len(f('x')) for _ in range(3)) == 3

# --- real f-string / t-string literals must still lex correctly ---
assert f'{5}' == '5'
tpl = t'x{5}y'
assert tpl is not None

print("test_fstring_prefix_name_regression passed")
