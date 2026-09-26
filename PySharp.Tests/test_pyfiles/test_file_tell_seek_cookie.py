"""Verifies text-mode tell() returns a seek cookie tracking the consumed byte position and that seek(cookie) restores the exact read state (issues #118/#311).

Covers \r\n folding, multi-byte and chunk-refill reads, astral code points, errors='ignore', truncated BOM and multi-byte tails, BCL codecs, and utf-16 BOM accounting.

:kind: test
"""

# Regression (issues #118/#311): text-mode tell() must return a seek cookie
# tracking the consumed byte position — not the underlying stream's physical
# position, which sits at EOF as soon as the readahead buffer fills. A clean
# decoder state packs the cookie to the plain byte offset, so the literal
# values match CPython; seek(cookie) restores the exact read state (the
# roundtrip holds across \r\n folding, multi-byte sequences and chunk
# refills), which the sweep below pins for every step of a large read.
open("_t118.txt", "w", newline="").write("line1\nline2\nline3")

f = open("_t118.txt", newline="")
f.read(5)
assert f.tell() == 5, f.tell()
f.seek(0)
assert f.readline() == "line1\n"
assert f.tell() == 6, f.tell()
f.seek(0)
f.read(5)
assert f.tell() == 5
f.seek(f.tell())
assert f.read(1) == "\n"
f.close()

# issue #311's exact sequence
open("_t311.txt", "w", newline="").write("abcdef")
f = open("_t311.txt", newline="")
assert f.tell() == 0
assert f.read(2) == "ab"
assert f.tell() == 2
f.seek(0)
assert f.read(3) == "abc"
f.close()

# \r\n folding: readline consumes both bytes, the cookie counts them
open("_t118crlf.txt", "w", newline="").write("a\r\nb")
f = open("_t118crlf.txt")
assert f.readline() == "a\n"
assert f.tell() == 3, f.tell()
f.seek(f.tell())
assert f.read(1) == "b"
f.close()

# multi-byte: the cookie is the byte offset, not the character count
open("_t118mb.txt", "w", encoding="utf-8", newline="").write("héllo wörld")
f = open("_t118mb.txt")
assert f.read(1) == "h"
assert f.tell() == 1
assert f.read(1) == "é"
assert f.tell() == 3, f.tell()
f.seek(1)
assert f.read(1) == "é"
f.close()

# chunk refill: files larger than the 8192-byte raw buffer with multi-byte
# characters, \r\n and a pending '\r' straddling the boundary — every tell()
# must roundtrip through seek() for every read step
filler = "x" * 8189
open("_t118big.txt", "w", newline="").write(filler + "𝄞\r\nz\r")
f = open("_t118big.txt")
assert f.read(100) == "x" * 100
assert f.tell() == 100
f.seek(0)
while True:
    pos = f.tell()
    s = f.read(13)
    if not s:
        break
    f.seek(pos)
    assert f.read(13) == s, pos
f.close()
for fillerLen, tail in ((8189, "𝄞\r\nz\r"), (8190, "é\r\ny"), (8191, "\r\ny\r")):
    # each tail's first character or terminator straddles the 8192 boundary
    open("_t118big2.txt", "w", newline="").write("x" * fillerLen + tail)
    f = open("_t118big2.txt")
    while True:
        pos = f.tell()
        s = f.readline()
        if not s:
            break
        f.seek(pos)
        assert f.readline() == s, pos
    f.close()

# astral code points: read(n) counts code points, and seek(tell()) never
# lands between the surrogate halves
open("_t118astral.bin", "wb").write("𝄞b".encode())
f = open("_t118astral.bin", encoding="utf-8")
assert f.read(1) == "𝄞", f.read(1)
assert f.tell() == 4, f.tell()
f.seek(f.tell())
assert f.read() == "b"
f.close()
open("_t118astral2.bin", "wb").write("𝄞a".encode())
f = open("_t118astral2.bin", encoding="utf-8")
assert f.read(2) == "𝄞a", f.read(2)
f.close()

# errors='ignore': silently consumed bytes still advance the cookie, so
# seek(tell()) never replays skipped data
open("_t118ign.bin", "wb").write(b"a\x80\x81b")
f = open("_t118ign.bin", errors="ignore")
assert f.read() == "ab"
assert f.tell() == 4, f.tell()
f.seek(f.tell())
assert f.read() == ""
f.close()

# readline(n) treats n as a hard cap: a '\r\n' terminator split by the
# budget leaves its '\n' to the next call
open("_t118split.bin", "wb").write(b"a\r\nb")
f = open("_t118split.bin", newline="")
assert f.readline(2) == "a\r"
assert f.readline(3) == "\n"
assert f.readline() == "b"
f.close()
f = open("_t118split.bin", newline="\r\n")
assert f.readline(2) == "a\r"
assert f.readline() == "\nb"
f.close()

# a truncated BOM prefix at end-of-file reads as '' (not a decode error)
open("_t118sigp.bin", "wb").write(b"\xef")
f = open("_t118sigp.bin", encoding="utf-8-sig")
assert f.read() == ""
assert f.tell() == 1, f.tell()
f.close()
open("_t118sigp.bin", "wb").write(b"\xef\xbb")
f = open("_t118sigp.bin", encoding="utf-8-sig")
assert f.read() == ""
assert f.tell() == 2, f.tell()
f.close()

# a truncated multi-byte tail at EOF raises UnicodeDecodeError
open("_t118trunc.txt", "wb").write(b"ab\xe4\xb8")
try:
    open("_t118trunc.txt", encoding="utf-8").read()
except UnicodeDecodeError as e:
    assert e.reason == "unexpected end of data", e.reason
else:
    raise AssertionError("expected UnicodeDecodeError")

# the resync forms keep returning positions
f = open("_t118.txt", newline="")
assert f.seek(0, 2) == 17
assert f.tell() == 17
assert f.read() == ""
f.seek(0)
f.read(2)
assert f.seek(0, 1) == 2
assert f.readline() == "ne1\n"
f.close()

# utf-8-sig: the stripped BOM still counts toward the cookie; before the
# first read nothing is stripped, seek(0) strips again
open("_t118sig.txt", "wb").write(b"\xef\xbb\xbfhi there\nx\n")
f = open("_t118sig.txt", encoding="utf-8-sig")
assert f.tell() == 0, f.tell()
assert f.read(1) == "h"
assert f.tell() == 4, f.tell()
f.seek(f.tell())
assert f.read() == "i there\nx\n"
f.close()
f = open("_t118sig.txt", encoding="utf-8-sig")
assert f.readline() == "hi there\n"
assert f.tell() == 12, f.tell()
f.seek(0)
assert f.tell() == 0, f.tell()
assert f.read() == "hi there\nx\n"
f.close()

# binary readline(n) consumes exactly min(n, line length) bytes; readline(0)
# consumes nothing
open("_t118brl.bin", "wb").write(b"abcdef")
f = open("_t118brl.bin", "rb")
assert f.readline(3) == b"abc"
assert f.tell() == 3, f.tell()
assert f.readline(-1) == b"def"
f.close()
f = open("_t118brl.bin", "rb")
assert f.readline(0) == b""
assert f.tell() == 0, f.tell()
assert f.readline(0) == b""
assert f.tell() == 0, f.tell()
assert f.readline(100) == b"abcdef"
assert f.tell() == 6, f.tell()
f.close()

# utf-16: the sniffed BOM is part of the first event's consumed bytes
open("_t118u16.txt", "wb").write(b"\xff\xfeh\x00i\x00")
f = open("_t118u16.txt", encoding="utf-16")
assert f.read(1) == "h"
assert f.tell() == 4, f.tell()
f.seek(f.tell())
assert f.read() == "i"
f.close()

# consecutive '\r': each owes its own translated '\n' and the byte
# positions stay exact through the fold
open("_t118crcr.txt", "wb").write(b"a\r\r\nb\rc\r")
f = open("_t118crcr.txt")
assert f.readline() == "a\n"
# the second '\r' stays pending here, so the cookie is opaque (CPython
# packs a need_eof flag into its own value); the roundtrip is the contract
t = f.tell()
f.seek(t)
assert f.read() == "\nb\nc\n"
f.close()
f = open("_t118crcr.txt")
assert f.readlines() == ["a\n", "\n", "b\n", "c\n"], f.readlines()
assert f.tell() == 8, f.tell()
f.close()
f = open("_t118crcr.txt")
assert f.readline() == "a\n"
assert f.readline() == "\n"
# the fold of the second '\r\n' completed: the state is clean again
assert f.tell() == 4, f.tell()
f.seek(f.tell())
assert f.read() == "b\nc\n"
f.close()

# BCL-codec (width-predicted) events obey the one-code-point contract:
# tell() advances one code point at a time and seek(tell()) never drops one
open("_t118cp.txt", "wb").write(b"0123456789abcdefghij")
f = open("_t118cp.txt", encoding="cp1252")
tells = []
for _ in range(6):
    f.read(1)
    tells.append(f.tell())
assert tells == [1, 2, 3, 4, 5, 6], tells
f.seek(f.tell())
assert f.read() == "6789abcdefghij"
f.close()
open("_t118gbk.bin", "wb").write("次次次次次".encode("gbk"))
f = open("_t118gbk.bin", encoding="gbk")
assert f.read(1) == "次"
t = f.tell()
assert t == 2, t
f.seek(t)
assert f.read() == "次次次次"
f.close()
# universal newlines fold through BCL codecs too
open("_t118nl.bin", "wb").write("a\rb\rc\r\n d".encode("cp1252"))
f = open("_t118nl.bin", encoding="cp1252")
assert f.read() == "a\nb\nc\n d", f.read()
f.close()
f = open("_t118nl.bin", encoding="cp1252")
assert f.readlines() == ["a\n", "b\n", "c\n", " d"]
f.close()
# a surrogate pair straddling the window boundary with surrogatepass stays
# one code point mid-stream
open("_t118sp.bin", "wb").write(("a" * 4095 + "𐀀z").encode("utf-16-le"))
f = open("_t118sp.bin", encoding="utf-16-le", errors="surrogatepass")
assert f.read(4095) == "a" * 4095
assert f.read(1) == "𐀀", f.read(1)
assert f.read() == "z"
f.close()
