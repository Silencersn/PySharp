"""Codec-name registration for the CJK and euro-variant encodings.

CPython 3.14 reference: Modules/cjkcodecs/_codecs_cn.c for the HZ state
machine (~{ / ~} sections over 7-bit GB2312 pairs), Modules/cjkcodecs/
_codecs_hk.c plus the generated big5hkscs table for Big5-HKSCS, and
Lib/encodings/aliases.py for the alias families. euc_jp, euc_kr,
iso2022_jp, iso2022_kr, tis_620, euc_jis_2004 and iso8859_15 map to the
platform codepages byte for byte; hz and big5hkscs report their errors
under their own registry names with single-byte error events.

:kind: test
"""


def enc(s, name):
    return list(s.encode(name))


def dec(data, name, errors='strict'):
    try:
        return data.decode(name, errors)
    except UnicodeDecodeError as e:
        return (e.encoding, e.start, e.end, e.reason)


# --- the seven codepage-backed names encode byte for byte -------------------
enc_results = [
    enc('漢字', 'euc_jp'),
    enc('カタカナ', 'euc_jp'),
    enc('ﾊﾝ', 'euc_jp'),
    enc('日本語テスト', 'euc_jp'),
    enc('한국어', 'euc_kr'),
    enc('대한민국', 'euc_kr'),
    enc('漢字', 'iso2022_jp'),
    enc('カタカナ', 'iso2022_jp'),
    enc('ABC漢字', 'iso2022_jp'),
    enc('한국어', 'iso2022_kr'),
    enc('ABC한글', 'iso2022_kr'),
    enc('กขค', 'tis_620'),
    enc('ทดสอบ', 'tis_620'),
    enc('漢字', 'euc_jis_2004'),
    enc('カタカナ', 'euc_jis_2004'),
    enc('café €', 'iso8859_15'),
    enc('Ünïcödé', 'iso8859_15'),
]

enc_expected = [
    [180, 193, 187, 250],
    [165, 171, 165, 191, 165, 171, 165, 202],
    [142, 202, 142, 221],
    [198, 252, 203, 220, 184, 236, 165, 198, 165, 185, 165, 200],
    [199, 209, 177, 185, 190, 238],
    [180, 235, 199, 209, 185, 206, 177, 185],
    [27, 36, 66, 52, 65, 59, 122, 27, 40, 66],
    [27, 36, 66, 37, 43, 37, 63, 37, 43, 37, 74, 27, 40, 66],
    [65, 66, 67, 27, 36, 66, 52, 65, 59, 122, 27, 40, 66],
    [27, 36, 41, 67, 14, 71, 81, 49, 57, 62, 110, 15],
    [65, 66, 67, 27, 36, 41, 67, 14, 71, 81, 49, 91, 15],
    [161, 162, 164],
    [183, 180, 202, 205, 186],
    [180, 193, 187, 250],
    [165, 171, 165, 191, 165, 171, 165, 202],
    [99, 97, 102, 233, 32, 164],
    [220, 110, 239, 99, 246, 100, 233],
]

for got, want in zip(enc_results, enc_expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

# the alias spellings normalize to the same codecs
assert '漢字'.encode('euc-jp') == '漢字'.encode('euc_jp')
assert '한국어'.encode('euckr') == '한국어'.encode('euc_kr')
assert '漢字'.encode('iso-2022-jp') == '漢字'.encode('iso2022_jp')
assert 'café'.encode('latin9') == 'café'.encode('iso8859_15')
assert b'\xb4\xc1\xbb\xfa'.decode('euc_jp') == '漢字'
assert b'\xc7\xd1\xb1\xb9\xbe\xee'.decode('euc_kr') == '한국어'

# --- hz: escape sections over 7-bit GB2312 pairs ----------------------------
hz_results = [
    enc('你好', 'hz'),
    enc('a你好', 'hz'),
    enc('你好a', 'hz'),
    enc('~', 'hz'),
    enc('a~你', 'hz'),
    enc('', 'hz'),
]

hz_expected = [
    [126, 123, 68, 99, 58, 67, 126, 125],
    [97, 126, 123, 68, 99, 58, 67, 126, 125],
    [126, 123, 68, 99, 58, 67, 126, 125, 97],
    [126, 126],
    [97, 126, 126, 126, 123, 68, 99, 126, 125],
    [],
]

for got, want in zip(hz_results, hz_expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

assert dec(b'~{Dc:O~}', 'hz') == '你合'
assert dec(b'~{Dc~}AB', 'hz') == '你AB'
assert dec(b'a~~b', 'hz') == 'a~b'
assert dec(b'~{Dc:O', 'hz') == '你合'
assert dec(b'~{!!', 'hz') == '\u3000'

hz_errors = [
    dec(b'~x', 'hz'),
    dec(b'~', 'hz'),
    dec(b'a~', 'hz'),
    dec(b'~{D', 'hz'),
    dec(b'~{Dc!', 'hz'),
    dec(b'~{~~', 'hz'),
    dec(b'~{\nDc~}', 'hz'),
    dec(b'~~{Dc~}', 'hz'),
    dec(b'~{}', 'hz'),
    dec(b'~{"!', 'hz'),
    dec(b'~{Dc~{', 'hz'),
    dec(b'\x80', 'hz'),
    dec(b'\xc4\xe3', 'hz'),
]

hz_error_expected = [
    ('hz', 0, 1, 'illegal multibyte sequence'),
    ('hz', 0, 1, 'incomplete multibyte sequence'),
    ('hz', 1, 2, 'incomplete multibyte sequence'),
    ('hz', 2, 3, 'incomplete multibyte sequence'),
    ('hz', 4, 5, 'incomplete multibyte sequence'),
    ('hz', 2, 3, 'illegal multibyte sequence'),
    ('hz', 2, 3, 'illegal multibyte sequence'),
    ('hz', 5, 6, 'illegal multibyte sequence'),
    ('hz', 2, 3, 'incomplete multibyte sequence'),
    ('hz', 2, 3, 'illegal multibyte sequence'),
    ('hz', 4, 5, 'illegal multibyte sequence'),
    ('hz', 0, 1, 'illegal multibyte sequence'),
    ('hz', 0, 1, 'illegal multibyte sequence'),
]

for got, want in zip(hz_errors, hz_error_expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

# non-GB2312 characters are unencodable, handlers fill the gap
try:
    '漢你'.encode('hz')
    assert False, "expected UnicodeEncodeError"
except UnicodeEncodeError as e:
    assert e.encoding == 'hz', e.encoding
    assert (e.start, e.end) == (0, 1), (e.start, e.end)
    assert e.reason == 'illegal multibyte sequence', e.reason

try:
    'ABC漢字'.encode('hz')
    assert False, "expected UnicodeEncodeError"
except UnicodeEncodeError as e:
    assert (e.start, e.end) == (3, 4), (e.start, e.end)

assert list('漢你'.encode('hz', 'ignore')) == [126, 123, 68, 99, 126, 125]
assert list('漢你'.encode('hz', 'replace')) == [63, 126, 123, 68, 99, 126, 125]
assert list('漢你'.encode('hz', 'backslashreplace')) == [92, 117, 54, 102, 50, 50, 126, 123, 68, 99, 126, 125]

assert '你好，世界！'.encode('hz').decode('hz') == '你好，世界！'
assert '中文测试 abc 123'.encode('hzgb').decode('hz-gb-2312') == '中文测试 abc 123'

# --- big5hkscs: Big5 plus the Hong Kong extensions --------------------------
bhk_results = [
    enc('漢字', 'big5hkscs'),
    enc('abc', 'big5hkscs'),
    enc('／', 'big5hkscs'),
    enc('十', 'big5hkscs'),
    enc('═', 'big5hkscs'),
    enc('\u31c0', 'hkscs'),
]

bhk_expected = [
    [186, 126, 166, 114],
    [97, 98, 99],
    [162, 65],
    [164, 81],
    [249, 249],
    [136, 64],
]

for got, want in zip(bhk_results, bhk_expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

assert dec(b'\x88\x40', 'big5hkscs') == '\u31c0'
assert dec(b'\x88\xa1', 'big5hkscs') == '\u01dc'
# an HKSCS extension mapping beyond the BMP
assert dec(b'\x8e\x64', 'big5hkscs') == '\U00025bb4'

bhk_errors = [
    dec(b'\x80\x40', 'big5hkscs'),
    dec(b'\x81\x40', 'big5hkscs'),
    dec(b'\xc4\x20', 'big5hkscs'),
    dec(b'\xc4', 'big5hkscs'),
    dec(b'\xa1\x7f', 'big5hkscs'),
    dec(b'\xff\xff', 'big5hkscs'),
]

bhk_error_expected = [
    ('big5hkscs', 0, 1, 'illegal multibyte sequence'),
    ('big5hkscs', 0, 1, 'illegal multibyte sequence'),
    ('big5hkscs', 0, 1, 'illegal multibyte sequence'),
    ('big5hkscs', 0, 1, 'incomplete multibyte sequence'),
    ('big5hkscs', 0, 1, 'illegal multibyte sequence'),
    ('big5hkscs', 0, 1, 'illegal multibyte sequence'),
]

for got, want in zip(bhk_errors, bhk_error_expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

# a recovered error resumes at the lead byte's successor
assert dec(b'\xc4\x20', 'big5hkscs', 'ignore') == ' '

try:
    '\uca0a'.encode('big5hkscs')
    assert False, "expected UnicodeEncodeError"
except UnicodeEncodeError as e:
    assert e.encoding == 'big5hkscs', e.encoding
    assert (e.start, e.end) == (0, 1), (e.start, e.end)
    assert e.reason == 'illegal multibyte sequence', e.reason

assert '漢字測試，中文！'.encode('big5hkscs').decode('big5hkscs') == '漢字測試，中文！'

print("test_cjk_codec_names passed")
