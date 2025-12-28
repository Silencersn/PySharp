def memoize(func):
    cache = {}
    def wrapper(n):
        if n in cache:
            return cache[n]
        result = func(n)
        cache[n] = result
        return result
    return wrapper

@memoize
def fibonacci_memo(n):
    assert n >= 0
    if n < 2:
        return n
    return fibonacci_memo(n-1) + fibonacci_memo(n-2)

fib_results = [fibonacci_memo(i) for i in range(10)]
assert fib_results == [0, 1, 1, 2, 3, 5, 8, 13, 21, 34]
