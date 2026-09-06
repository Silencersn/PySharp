# encode() errors-handler regression: CPython resolves the handler name
# lazily at the first actual encoding error (unicode_encode_call_errorhandler
# -> PyCodec_LookupError). A successful encoding never consults the name;
# an unknown name raises LookupError "unknown error handler name '...'"
# only when an error happens.

# no error: unknown handler names are inert
assert "a".encode("ascii", "bogus") == b"a"
assert "a".encode("utf-8", "bogus") == b"a"
assert "a".encode("ascii", "") == b"a"

# actual error: LookupError with the CPython message, catchable as
# LookupError but not as ValueError
try:
    "é".encode("ascii", "bogus")
except LookupError as e:
    assert type(e).__name__ == "LookupError", type(e).__name__
    assert str(e) == "unknown error handler name 'bogus'", str(e)
else:
    raise AssertionError("LookupError expected")

try:
    "é".encode("ascii", "")
except ValueError:
    raise AssertionError("LookupError must not be a ValueError")
except LookupError as e:
    assert str(e) == "unknown error handler name ''", str(e)
else:
    raise AssertionError("LookupError expected")

# built-in handlers keep working on both paths
assert "a".encode("ascii", "replace") == b"a"
assert "é".encode("ascii", "replace") == b"?"
assert "éa".encode("ascii", "ignore") == b"a"
assert "é".encode("ascii", "xmlcharrefreplace") == b"&#233;"
assert "é".encode("ascii", "backslashreplace") == b"\\xe9"
assert "é".encode("ascii", "namereplace") == b"\\N{LATIN SMALL LETTER E WITH ACUTE}"

print("ok")
