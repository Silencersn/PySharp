"""
Regression: str.encode error handlers 'xmlcharrefreplace' / 'backslashreplace'
/ 'namereplace' must produce the CPython escape sequences (not a plain '?'),
and the legal encoding name 'utf-16-le' must be accepted.

CPython 3.14 reference:
    'é'.encode('ascii', 'xmlcharrefreplace')  -> b'&#233;'
    'é'.encode('ascii', 'backslashreplace')   -> b'\\xe9'
    '中'.encode('ascii', 'backslashreplace')  -> b'\\u4e2d'
    'é'.encode('ascii', 'namereplace')        -> b'\\N{LATIN SMALL LETTER E WITH ACUTE}'
    'é'.encode('ascii', 'ignore')             -> b''
    'é'.encode('ascii', 'replace')            -> b'?'
    'abc'.encode('utf-16-le')                 -> b'a\x00b\x00c\x00'

Previously the three escape-style handlers set a fallback on a throwaway
encoder but then called Encoding.GetBytes (which uses the encoding's default
replacement fallback), so all three silently emitted b'?'; 'utf-16-le' was
not a recognized .NET encoding name and was misreported as unknown.
"""

# escape-style handlers
assert 'é'.encode('ascii', 'xmlcharrefreplace') == b'&#233;'
assert 'é'.encode('ascii', 'backslashreplace') == b'\\xe9'
assert '中'.encode('ascii', 'backslashreplace') == b'\\u4e2d'
assert 'é'.encode('ascii', 'namereplace') == b'\\N{LATIN SMALL LETTER E WITH ACUTE}'

# simple handlers keep working
assert 'é'.encode('ascii', 'ignore') == b''
assert 'é'.encode('ascii', 'replace') == b'?'
assert 'abc'.encode('ascii') == b'abc'

# legal encoding names keep working
assert 'abc'.encode('utf-16-le') == b'a\x00b\x00c\x00'
assert 'abc'.encode('utf-8') == b'abc'

print("test_str_encode_error_handlers_regression passed")
