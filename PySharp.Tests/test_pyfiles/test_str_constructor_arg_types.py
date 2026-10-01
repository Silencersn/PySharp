"""str() checks encoding/errors types even without an object argument.

:kind: test
"""

# Regression: the type checks sat inside the has-object branch, so
# str(encoding=1) silently returned ''. CPython's clinic resolves the
# declared-str parameters before unicode_new ever looks at object. The
# rejected-None rendering also splits by entry path: positional reports
# "NoneType" (tp_name), keyword reports "None" (converterr).

def try_str(label, fn):
    try:
        print(label, "=", repr(fn()))
    except Exception as e:
        print(label, "!", type(e).__name__ + ":", e)

for label, fn in [
    ("enc-int", lambda: str(encoding=1)),
    ("err-int", lambda: str(errors=1)),
    ("enc-none", lambda: str(encoding=None)),
    ("err-none", lambda: str(errors=None)),
    ("enc-list", lambda: str(encoding=[], errors=1)),
    ("empty", lambda: str()),
    ("enc-ok", lambda: str(encoding="utf-8")),
    ("err-ok", lambda: str(errors="strict")),
    ("both-ok", lambda: str(encoding="utf-8", errors="strict")),
    ("obj-bytes", lambda: str(b"a")),
    ("bytes-enc", lambda: str(b"a", "utf-8")),
    ("bytes-int", lambda: str(b"a", 1)),
    ("bytes-kwenc-int", lambda: str(b"a", encoding=1)),
    ("three-int", lambda: str(1, 2, 3)),
    ("obj-kw", lambda: str(object=1)),
    ("pos-none", lambda: str(b"a", None)),
    ("kwenc-none", lambda: str(b"a", encoding=None)),
    ("kwerr-none", lambda: str(b"a", errors=None)),
    ("unknown-kw", lambda: exec("str(x=1)", {})),
    ("bytes-decode-ok", lambda: b"a".decode()),
]:
    try_str(label, fn)
