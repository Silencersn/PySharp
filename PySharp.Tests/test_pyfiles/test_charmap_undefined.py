"""
CPython refuses the bytes its single-byte charmap codecs leave undefined
(issue #344): 'charmap' codec can't decode/encode, character maps to
<undefined>.
Tests:
- cp1252/cp1250 undefined positions: strict decode/encode errors (0-1)
- errors= handlers on both sides: replace/ignore/backslashreplace/
  surrogateescape/xmlcharrefreplace, one U+FFFD per byte
- reverse controls: 0x8d/0x8f/0x9d are defined in cp1250
- same fix across cp1251/cp1253/cp1254/cp1257/cp1258/cp857/cp874/
  cp869/iso-8859-3/iso-8859-6/kz1048 and the windows-1252 alias
- defined positions and latin-1 round trips stay untouched
- surrogateescape round trip of the undefined byte

:kind: test
"""
import sys

def run(expr):
    try:
        v = eval(expr)
        if isinstance(v, bytes):
            v = repr(v)
        print(expr, "=>", repr(v))
    except Exception as e:
        print(expr, "=>", type(e).__name__, str(e))

# --- cp1252: the issue's exact cases ---
run("bytes([0x81]).decode('cp1252')")
run("b'\x8d'.decode('cp1252')")
run("b'\x8f'.decode('cp1252')")
run("b'\x90'.decode('cp1252')")
run("b'\x9d'.decode('cp1252')")
run("bytes([0x81]).decode('cp1252', 'strict')")
run("bytes([0x81]).decode('cp1252', 'replace')")
run("bytes([0x81]).decode('cp1252', 'ignore')")
run("bytes([0x81]).decode('cp1252', 'backslashreplace')")
run("bytes([0x81]).decode('cp1252', 'surrogateescape')")
run("bytes([0x81, 0x8d]).decode('cp1252', 'replace')")
run("b'\x81ok\x9d'.decode('cp1252', 'replace')")
run("'\x81'.encode('cp1252')")
run("'\x81\x8d'.encode('cp1252')")
run("'\x81'.encode('cp1252', 'ignore')")
run("'\x81'.encode('cp1252', 'replace')")
run("'a\x81b'.encode('cp1252', 'backslashreplace')")
run("'\x81'.encode('cp1252', 'xmlcharrefreplace')")
run("'a\x81b'.encode('cp1252')")
# defined positions still work both ways
run("bytes([0x80]).decode('cp1252')")
run("bytes([0x8c]).decode('cp1252')")
run("bytes([0x9c]).decode('cp1252')")
run("'\u20ac'.encode('cp1252')")
run("'caf\u00e9'.encode('cp1252')")

# --- cp1250 ---
run("bytes([0x81]).decode('cp1250')")
run("'\x81'.encode('cp1250')")
# the issue's reverse controls: 0x8d/0x8f/0x9d are defined in cp1250
run("bytes([0x8d]).decode('cp1250')")
run("bytes([0x8f]).decode('cp1250')")
run("bytes([0x9d]).decode('cp1250')")
run("'\u0164'.encode('cp1250')")
run("bytes([0x83]).decode('cp1250')")
run("bytes([0x88]).decode('cp1250')")
run("bytes([0x90]).decode('cp1250')")
run("bytes([0x98]).decode('cp1250')")

# --- round trip of the undefined byte itself (surrogateescape) ---
run("b'\x81'.decode('cp1252', 'surrogateescape').encode('cp1252', 'surrogateescape')")

# --- other families ---
run("bytes([0x98]).decode('cp1251')")
run("bytes([0x81]).decode('cp1254')")
run("bytes([0x98]).decode('cp1258')")
run("'\x98'.encode('cp1251')")
run("bytes([0x81]).decode('windows-1252')")
run("bytes([0x81]).decode('cp1253')")
run("bytes([0xd5]).decode('cp857')")
run("'\u00d5'.encode('cp857')")
run("'\u00d7'.encode('cp857')")
run("bytes([0xdb]).decode('cp874')")
run("bytes([0xa1]).decode('iso-8859-6')")
run("bytes([0xa1]).decode('iso-8859-3')")
run("bytes([0x98]).decode('kz1048')")

# --- unaffected codecs still fine ---
run("'hello'.encode('cp1252')")
run("b'hello'.decode('cp1252')")
run("'gr\u00fc\u00dfe'.encode('cp1250')")
run("b'caf\xe9'.decode('latin-1')")
run("bytes([0x81]).decode('latin-1')")
run("'\u0081'.encode('latin-1')")
print("done")
