"""reversed() and iter() name their iterator types after the sequence: list_reverseiterator, range_iterator reused backwards, and str_ascii_iterator for ascii strings.

:kind: test
"""

# Regression: reversed(list) reports the dedicated list_reverseiterator
# type, reversed(range) reuses the forward range_iterator type (the two
# type() results are identical), and iter() over an all-ascii string
# reports str_ascii_iterator while any non-ascii kind falls back to
# str_iterator. Sequences without a dedicated reverse iterator (str,
# tuple, bytes) keep the generic reversed type.


def show(label, f):
    try:
        print(label, "=", f())
    except BaseException as e:
        print(label, "!", type(e).__name__, e)


show("reversed_list", lambda: type(reversed([1, 2])).__name__)
show("reversed_range", lambda: type(reversed(range(3))).__name__)
show("reversed_range_eq_iter_type", lambda: type(reversed(range(3))) is type(iter(range(3))))
show("iter_str_ascii", lambda: type(iter("a")).__name__)
show("iter_str_nonascii", lambda: type(iter("ä")).__name__)
show("iter_str_surrogate", lambda: type(iter("\ud800")).__name__)
show("repr_rl", repr(reversed([1, 2])))
show("reversed_list_values", lambda: list(reversed([1, 2, 3])))
show("reversed_range_values", lambda: list(reversed(range(3))))
show("reversed_long_range", lambda: type(reversed(range(2**70))).__name__)
show("reversed_long_range_vals", lambda: list(reversed(range(2**70, 2**70 + 3))))
show("reversed_tuple", lambda: type(reversed((1, 2))).__name__)
show("reversed_str", lambda: type(reversed("ab")).__name__)
show("reversed_bytes", lambda: type(reversed(b"ab")).__name__)
show("iter_list", lambda: type(iter([1])).__name__)
show("iter_range", lambda: type(iter(range(2))).__name__)
show("empty_range_rev", lambda: list(reversed(range(0))))
show("neg_step_range_rev", lambda: list(reversed(range(10, 0, -2))))


def iter_mutation():
    l = [1, 2, 3]
    r = reversed(l)
    out = [next(r)]
    l.clear()
    try:
        out.append(next(r))
    except StopIteration:
        out.append("stopped")
    return out


show("reversed_list_mutation", iter_mutation)


def rev_partial():
    r = reversed([1, 2, 3])
    next(r)
    return list(r)


show("reversed_list_partial", rev_partial)
