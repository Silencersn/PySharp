"""
Regression: frozenset hash must be order-independent (CPython
frozenset_hash). The old fold mixed element hashes with a
position-dependent chain, so equal frozensets built in different orders
hashed differently. The new fold ports CPython's algorithm exactly, so
int-only frozensets even reproduce CPython's numeric hash values.
Also covers hash() keeping user __hash__ results within the Py_hash_t
(int64) range exactly, as CPython slot_tp_hash does.
"""

# exact values (deterministic: int elements only)
assert hash(frozenset()) == 133146708735736
assert hash(frozenset([0])) == -2704248722033767810
assert hash(frozenset([1])) == -558064481276695278
assert hash(frozenset([2])) == 4774279697163166305
assert hash(frozenset([3])) == 6578559351554755696
assert hash(frozenset([1, 2])) == -1826646154956904602
assert hash(frozenset([1, 2, 3])) == -272375401224217160
assert hash(frozenset([True, False])) == 9000494312501845833
assert hash(frozenset([2 ** 61 - 1, 2 ** 61])) == 9000494312501845833
assert hash(frozenset([10 ** 30])) == 4163646982626012307
assert hash(frozenset([frozenset([1, 2]), frozenset([3])])) == -5902812605336009127
assert hash(frozenset(range(50))) == -1309786936372247613

# order independence over all permutations
def perms(xs):
    if len(xs) <= 1:
        yield xs
        return
    for i in range(len(xs)):
        for p in perms(xs[:i] + xs[i + 1:]):
            yield [xs[i]] + p

hashes = {hash(frozenset(p)) for p in perms([1, 2, 3, 4, 5])}
assert len(hashes) == 1, hashes

# order independence across construction paths
assert hash(frozenset({1, 2, 3})) == hash(frozenset([3, 2, 1]))
assert hash(frozenset(frozenset([4, 5]))) == hash(frozenset([5, 4]))
big = frozenset(range(20))
assert hash(big) == hash(frozenset(reversed(list(big))))

# stable across calls, never the -1 sentinel
mixed = frozenset(["a", None, (1, 2)])
h = hash(mixed)
assert h == hash(mixed)
assert h != -1
assert hash(frozenset([0, "b"])) != -1

# equal frozensets hash equal and work as dict keys
assert hash(frozenset([1, 2])) == hash(frozenset([2, 1]))
d = {frozenset([1, 2]): "x"}
assert d[frozenset([2, 1])] == "x"
assert frozenset([2, 1]) in d

# user __hash__ results within the Py_hash_t range are kept exactly;
# -1 maps to -2; wider values fall back to long hashing
class Big:
    def __hash__(self):
        return 2 ** 62

class NegOne:
    def __hash__(self):
        return -1

class Huge:
    def __hash__(self):
        return 10 ** 100

class Small:
    def __hash__(self):
        return 7

assert hash(Big()) == 2 ** 62
assert hash(NegOne()) == -2
assert hash(Huge()) == hash(10 ** 100)
assert hash(Small()) == 7

print("test_frozenset_hash_regression passed")
