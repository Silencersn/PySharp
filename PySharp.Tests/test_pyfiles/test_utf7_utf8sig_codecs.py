"""utf-7 and utf-8-sig codec conformance.

CPython 3.14 reference: Objects/unicodeobject.c (PyUnicode_DecodeUTF7Stateful
and _PyUnicode_EncodeUTF7 with the default option word: set O and whitespace
direct) and Lib/encodings/utf_8_sig.py for the BOM-stripping decoder. utf-7
reports decode errors under the bare 'utf7' name; utf-8-sig hands utf-8 the
BOM-sliced input, so error events carry the sliced bytes with
payload-relative positions.

:kind: test
"""


def enc(s):
    return list(s.encode('utf-7'))


def dec(data, errors='strict'):
    try:
        text = data.decode('utf-7', errors)
    except UnicodeDecodeError as e:
        return (e.encoding, e.start, e.end, e.reason)
    return [ord(c) for c in text]


# --- utf-7 encode: direct sets ride themselves, the rest shift base64 ------
enc_results = [
    enc('a b'),
    enc('a+b'),
    enc('A\u2260A'),
    enc('a\u2260b\u2260c'),
    enc(''),
    enc('!'),
    enc('"'),
    enc('a\nb'),
    enc('a\tb'),
    enc('a\rb'),
    enc('\U00010437'),
    enc('a\U00010437b'),
    enc('+'),
    enc('~'),
    enc('\\'),
    enc('a+b+c'),
    enc('\x00a'),
    enc('a\x80'),
    enc('a\u2212b'),
    enc('-a'),
    enc('a-'),
    enc('a/-b'),
]

enc_expected = [
    [97, 32, 98],
    [97, 43, 45, 98],
    [65, 43, 73, 109, 65, 45, 65],
    [97, 43, 73, 109, 65, 45, 98, 43, 73, 109, 65, 45, 99],
    [],
    [33],
    [34],
    [97, 10, 98],
    [97, 9, 98],
    [97, 13, 98],
    [43, 50, 65, 72, 99, 78, 119, 45],
    [97, 43, 50, 65, 72, 99, 78, 119, 45, 98],
    [43, 45],
    [43, 65, 72, 52, 45],
    [43, 65, 70, 119, 45],
    [97, 43, 45, 98, 43, 45, 99],
    [43, 65, 65, 65, 45, 97],
    [97, 43, 65, 73, 65, 45],
    [97, 43, 73, 104, 73, 45, 98],
    [45, 97],
    [97, 45],
    [97, 47, 45, 98],
]

for got, want in zip(enc_results, enc_expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

# --- utf-7 decode: shift sections, surrogate pairs, lone surrogates --------
dec_results = [
    dec(b'a b'),
    dec(b'a+-b'),
    dec(b'+AAA-'),
    dec(b'+ADw-'),
    dec(b'+2AA-'),
    dec(b'a+2AA-b'),
    dec(b'+2AHcNw-'),
    dec(b'ab+'),
    dec(b'+'),
    dec(b'+-'),
    dec(b'A+ImA-A'),
]

dec_expected = [
    [97, 32, 98],
    [97, 43, 98],
    [0],
    [60],
    [0xD800],
    [97, 0xD800, 98],
    [0x10437],
    [97, 98],
    [],
    [43],
    [65, 0x2260, 65],
]

for got, want in zip(dec_results, dec_expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

# --- utf-7 decode errors: the four events and their spans ------------------
error_results = [
    dec(b'+A'),
    dec(b'+AB'),
    dec(b'+AAx'),
    dec(b'a+AA-b'),
    dec(b'+AB-'),
    dec(b'+ABG-'),
    dec(b'a\xffb'),
    dec(b'\xff'),
    dec(b'+\xff'),
    dec(b'+.a'),
]

error_expected = [
    ('utf7', 0, 2, 'unterminated shift sequence'),
    ('utf7', 0, 3, 'unterminated shift sequence'),
    ('utf7', 0, 4, 'unterminated shift sequence'),
    ('utf7', 1, 5, 'partial character in shift sequence'),
    ('utf7', 0, 4, 'partial character in shift sequence'),
    ('utf7', 0, 5, 'non-zero padding bits in shift sequence'),
    ('utf7', 1, 2, 'unexpected special character'),
    ('utf7', 0, 1, 'unexpected special character'),
    ('utf7', 0, 2, 'ill-formed sequence'),
    ('utf7', 0, 2, 'ill-formed sequence'),
]

for got, want in zip(error_results, error_expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

# the error object is the full input bytes
try:
    b'+AB'.decode('utf-7')
    assert False, "expected UnicodeDecodeError"
except UnicodeDecodeError as e:
    assert e.object == b'+AB', e.object

# --- utf-7 errors= handlers -------------------------------------------------
assert dec(b'a+AA-b', 'ignore') == [97, 98]
assert dec(b'+A', 'ignore') == []
# the 18 queued bits emit one unit before the padding error fires
assert dec(b'+ABG-', 'ignore') == [17]
assert dec(b'a\xffb', 'replace') == [97, 0xFFFD, 98]

# --- utf-7 round trips ------------------------------------------------------
for s in ('\U00010437a!b\u2260c', 'Hello, World! \u2260 \u00f7 \u1234',
          'a+v a', '\t\n\r ', 'na\xefve'):
    assert s.encode('utf-7').decode('utf-7') == s, s

# alias spelling normalizes the same way
assert 'a\u2260b'.encode('utf_7') == 'a\u2260b'.encode('utf-7')
assert list(b'a+ImA-b'.decode('utf_7')) == list('a\u2260b')

# --- utf-8-sig: BOM on encode, one BOM stripped on decode -------------------
assert list('x'.encode('utf-8-sig')) == [239, 187, 191, 120]
assert list(''.encode('utf-8-sig')) == [239, 187, 191]
assert list('x'.encode('utf_8_sig')) == [239, 187, 191, 120]

assert b'\xef\xbb\xbf'.decode('utf-8-sig') == ''
assert b'\xef\xbb\xbf x'.decode('utf-8-sig') == ' x'
assert b'x'.decode('utf-8-sig') == 'x'
assert b'\xef\xbb\xbf\xef\xbb\xbf'.decode('utf-8-sig') == '\ufeff'
assert b'\xef\xbb\xbf x'.decode('utf_8_sig') == ' x'
assert str(b'\xef\xbb\xbf x', 'utf-8-sig') == ' x'

# a stripped decode delegates to utf-8: errors keep the utf-8 name, the
# sliced bytes as object and payload-relative positions
try:
    b'\xef\xbb\xbf\xff'.decode('utf-8-sig')
    assert False, "expected UnicodeDecodeError"
except UnicodeDecodeError as e:
    assert e.encoding == 'utf-8', e.encoding
    assert e.object == b'\xff', e.object
    assert (e.start, e.end) == (0, 1), (e.start, e.end)
    assert e.reason == 'invalid start byte', e.reason

try:
    b'\xef\xbb'.decode('utf-8-sig')
    assert False, "expected UnicodeDecodeError"
except UnicodeDecodeError as e:
    assert e.encoding == 'utf-8', e.encoding
    assert (e.start, e.end) == (0, 2), (e.start, e.end)
    assert e.reason == 'unexpected end of data', e.reason

# reverse controls: plain utf-8 keeps its exact behavior
assert b'\xef\xbb\xbf'.decode('utf-8') == '\ufeff'
assert 'x'.encode('utf-8') == b'x'

print("test_utf7_utf8sig_codecs passed")
