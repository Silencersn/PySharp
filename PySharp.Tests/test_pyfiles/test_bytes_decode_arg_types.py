"""bytes.decode rejects non-str encoding/errors with the argument name.

:kind: test
"""

# Regression: the checks were hardcoded sentences ("decoding must be
# str") and an explicit None slipped past them into the default. CPython
# 3.14 declares both parameters as strict str converters — the message
# carries the argument name and the actual type, with None rendered as
# "None" — matching the already-correct str.encode path.

def try_decode(label, fn):
    try:
        print(label, "=", repr(fn()))
    except Exception as e:
        print(label, "!", type(e).__name__ + ":", e)

for label, fn in [
    ("int", lambda: b"a".decode(1)),
    ("float", lambda: b"a".decode(1.5)),
    ("list", lambda: b"a".decode([])),
    ("bytes", lambda: b"a".decode(b"utf-8")),
    ("kw-int", lambda: b"a".decode(encoding=1)),
    ("errors-int", lambda: b"a".decode("utf-8", 1)),
    ("errors-kw", lambda: b"a".decode(errors=1)),
    ("none", lambda: b"a".decode(None)),
    ("errors-none", lambda: b"a".decode("utf-8", None)),
    ("ok", lambda: b"a".decode()),
    ("ok-enc", lambda: b"a".decode("utf-8")),
    ("ignore", lambda: b"\xff".decode("utf-8", "ignore")),
    ("replace", lambda: b"\xff".decode("utf-8", "replace")),
    ("strict", lambda: b"\xff".decode("utf-8")),
    ("encode-int", lambda: "a".encode(1)),
    ("encode-none", lambda: "a".encode(None)),
    ("encode-errors", lambda: "a".encode("utf-8", 1)),
    ("enc-kw", lambda: b"a".decode(encoding="utf-8", errors="ignore")),
]:
    try_decode(label, fn)
