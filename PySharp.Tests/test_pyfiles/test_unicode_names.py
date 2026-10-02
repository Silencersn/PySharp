"""
Unicode character names: the \\N{...} escape in literals (issue #318) and
the namereplace error handler (issue #343).
Tests:
- \\N{...} resolves formal names, aliases, algorithmic CJK/Hangul names
- case-insensitive lookup, astral characters, embedded escapes
- unknown name and malformed escapes report SyntaxError with position
- bytes literals keep \\N{...} literally
- f-strings keep a non-raw \\N{...} in the literal part
- namereplace emits \\N{NAME} for named code points, hex for unnamed ones
:kind: test
"""
import warnings

warnings.simplefilter("ignore", SyntaxWarning)


def run(expr):
    try:
        v = eval(expr)
        if isinstance(v, bytes):
            v = repr(v)
        print(expr, "=>", repr(v))
    except SyntaxError as e:
        print(expr, "=> SyntaxError", e.msg)
    except Exception as e:
        print(expr, "=>", type(e).__name__, str(e))

# --- issue 318: the \N escape in str literals ---
run("len('\\N{BULLET}')")
run("'\\N{BULLET}'")
run("repr('\\N{COPYRIGHT SIGN}')")
run("repr('\\N{GREEK SMALL LETTER ALPHA}')")
run("len('A\\N{BULLET}B')")
run("'A\\N{BULLET}B'")
run("'\\N{NBSP}' == '\\xa0'")
run("'\\N{ZWNJ}' == '\\u200c'")
run("'\\N{CJK UNIFIED IDEOGRAPH-4E2D}' == '\\u4e2d'")
run("'\\N{HANGUL SYLLABLE GA}' == '\\uac00'")
run("'\\N{degree sign}' == '\\N{DEGREE SIGN}'")
run("'\\N{GRINNING FACE}'")
run("len('\\N{GRINNING FACE}')")
run("'\\N{DEGREE SIGN}\\N{BULLET}'")
run("b'\\N{BULLET}'")
run("rb'\\N{BULLET}'")
run("f'\\N{BULLET}'")
run("f'\\N{DEGREE SIGN}C'")
run("f'x\\N{BULLET}y {1+1}'")

# errors: unknown name, malformed escape, empty name, no closing brace
run("'\\N{NOT A NAME}'")
run("'\\N{BULL}'")
run("len('\\N{BULL}')")
run("'x\\N{BULL}y'")
run("'\\N{}'")
run("'\\N{'")
run("'\\N'")
run("'\\N{BULLET")

# --- issue 343: namereplace ---
run("'\\u4e2d'.encode('ascii', 'namereplace')")
run("'\\U0001f600'.encode('ascii', 'namereplace')")
run("'\\u00ad'.encode('ascii', 'namereplace')")
run("'\\u0085'.encode('ascii', 'namereplace')")
run("'\\u0080'.encode('ascii', 'namereplace')")
run("'\\ue000'.encode('ascii', 'namereplace')")
run("'\\U0010ffff'.encode('ascii', 'namereplace')")
run("'\\U00017000'.encode('ascii', 'namereplace')")
run("'\\uac00'.encode('ascii', 'namereplace')")
run("'\\u3400'.encode('ascii', 'namereplace')")
run("'\\U0002a700'.encode('ascii', 'namereplace')")
run("'\\u2000'.encode('ascii', 'namereplace')")
run("'\\u0101'.encode('latin-1', 'namereplace')")
run("'\\u0150'.encode('latin-1', 'namereplace')")
run("'caf\\u00e9'.encode('ascii', 'namereplace')")
run("'\\xa0'.encode('ascii', 'namereplace')")
run("'\\u4e2d'.encode('ascii', 'namereplace').decode('ascii')")
print("done")
