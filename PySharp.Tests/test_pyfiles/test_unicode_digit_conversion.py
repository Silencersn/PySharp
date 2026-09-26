"""
float()/complex()/int() accept Nd (Unicode decimal digit)
strings, the way CPython's _PyUnicode_TransformDecimalAndSpaceToASCII pre-pass
does. ASCII text passes through, a non-ASCII space becomes ' ', every Nd code
point becomes its ASCII digit, and the first code point that is none of those
ends the transformed text with '?'.
Exercises PyUnicodeData.TransformDecimalAndSpaceToAscii and the string
argument entries of float(), complex() and int().

:kind: test
"""

ARABIC = '\u0661\u0662\u0663'              # 123
ARABIC_ZERO = '\u0660'
DEVANAGARI = '\u0967\u0968\u0969'          # 123
THAI = '\u0e51\u0e52\u0e53'                # 123
FULLWIDTH = '\uff11\uff12\uff13'           # 123
BOLD = '\U0001D7CF\U0001D7D0\U0001D7D1'    # 123, above the BMP
NBSP = '\u00a0'
IDEOGRAPHIC_SPACE = '\u3000'
VULGAR_HALF = '\u00bd'
ARABIC_THOUSANDS = '\u066c'


def expect_error(label, exc_type, expected, fn):
    try:
        fn()
    except exc_type as error:
        assert str(error) == expected, f'{label}: {str(error)!r}'
        return
    except Exception as error:
        raise AssertionError(f'{label}: {type(error).__name__}: {error}')

    raise AssertionError(f'{label}: no {exc_type.__name__}')


# --- int(): digits, signs and surrounding space ---
assert int(ARABIC) == 123
assert int(' ' + ARABIC + ' ') == 123
assert int('\t' + ARABIC + '\n') == 123
assert int(NBSP + ARABIC + NBSP) == 123
assert int('+' + ARABIC) == 123
assert int('-' + ARABIC) == -123
assert int(ARABIC_ZERO + ARABIC) == 123
assert int(ARABIC + ARABIC) == 123123
assert int(DEVANAGARI) == 123
assert int(THAI) == 123
assert int(FULLWIDTH) == 123
assert int(BOLD) == 123

# --- int(): bases see the transformed digits ---
assert int(ARABIC, 16) == 291
assert int(ARABIC, 8) == 83
assert int(ARABIC, 36) == 1371
assert int('0x' + ARABIC, 16) == 291
assert int(BOLD, 16) == 291
assert int(ARABIC + '_' + ARABIC) == 123123
expect_error('int(arabic, 2)', ValueError,
             f"invalid literal for int() with base 2: '{ARABIC}'", lambda: int(ARABIC, 2))

# --- int(): base 0 applies its literal rules to the transformed text ---
assert int(ARABIC, 0) == 123
assert int(ARABIC_ZERO + 'x10', 0) == 16
assert int(ARABIC_ZERO + 'b101', 0) == 5
assert int(ARABIC_ZERO + 'o17', 0) == 15
expect_error('int(nd zero 123, 0)', ValueError,
             f"invalid literal for int() with base 0: '{ARABIC_ZERO}123'",
             lambda: int(ARABIC_ZERO + '123', 0))
expect_error('int(nd zero 777, 0)', ValueError,
             f"invalid literal for int() with base 0: '{ARABIC_ZERO}777'",
             lambda: int(ARABIC_ZERO + '777', 0))
expect_error('int(nd zero _123, 0)', ValueError,
             f"invalid literal for int() with base 0: '{ARABIC_ZERO}_123'",
             lambda: int(ARABIC_ZERO + '_123', 0))

# --- int(): failures keep the CPython message and quote the original string ---
expect_error('int(nd dot nd)', ValueError,
             f"invalid literal for int() with base 10: '{ARABIC}.\u0664\u0665'",
             lambda: int(ARABIC + '.\u0664\u0665'))
expect_error('int(nd letter)', ValueError,
             f"invalid literal for int() with base 10: '{ARABIC}x'",
             lambda: int(ARABIC + 'x'))
expect_error('int(nd space nd)', ValueError,
             f"invalid literal for int() with base 10: '\u0661 \u0662'",
             lambda: int('\u0661 \u0662'))
expect_error('int(vulgar half)', ValueError,
             f"invalid literal for int() with base 10: '{VULGAR_HALF}'",
             lambda: int(VULGAR_HALF))

# --- float(): the whole numeric grammar accepts Nd digits ---
assert float(ARABIC) == 123.0
assert float(' ' + ARABIC + ' ') == 123.0
assert float('\t' + ARABIC + '\n') == 123.0
assert float(NBSP + ARABIC + NBSP) == 123.0
assert float(IDEOGRAPHIC_SPACE + ARABIC + IDEOGRAPHIC_SPACE) == 123.0
assert float('+' + ARABIC) == 123.0
assert float('-' + ARABIC) == -123.0
assert float('\u0661\u0662\u0663.\u0664\u0665') == 123.45
assert float('\u0661\u0662\u0663.') == 123.0
assert float('.\u0661\u0662\u0663') == 0.123
assert float(ARABIC_ZERO + '.\u0665') == 0.5
assert float('\u0661.\u0665e\u0662') == 150.0
assert float('\u0661e\u0661') == 10.0
assert float('\u0661e+\u0662') == 100.0
assert float(ARABIC + 'e\u0664') == 1230000.0
assert float(DEVANAGARI) == 123.0
assert float(THAI) == 123.0
assert float(FULLWIDTH) == 123.0
assert float(BOLD) == 123.0
assert float(NBSP + 'inf' + NBSP) == float('inf')

# --- float(): failures quote the original string ---
expect_error('float(nd j)', ValueError,
             f"could not convert string to float: '{ARABIC}j'",
             lambda: float(ARABIC + 'j'))
expect_error('float(nd thousands sep)', ValueError,
             f"could not convert string to float: '{ARABIC}{ARABIC_THOUSANDS}\u0664\u0665\u0666'",
             lambda: float(ARABIC + ARABIC_THOUSANDS + '\u0664\u0665\u0666'))
expect_error('float(nd vulgar half)', ValueError,
             f"could not convert string to float: '\u0662{VULGAR_HALF}'",
             lambda: float('\u0662' + VULGAR_HALF))
expect_error('float(nd space nd)', ValueError,
             f"could not convert string to float: '\u0661 \u0662'",
             lambda: float('\u0661 \u0662'))
expect_error('float(empty)', ValueError,
             "could not convert string to float: ''", lambda: float(''))
expect_error('float(spaces)', ValueError,
             "could not convert string to float: '   '", lambda: float('   '))

# --- float(): ASCII input is untouched by the transform ---
assert float('1.5') == 1.5
assert float(' 1e3 ') == 1000.0
assert float('-inf') == float('-inf')
assert float('nan') != float('nan')

# --- complex(): every documented string form accepts Nd digits ---
assert complex(ARABIC + 'j') == 123j
assert complex(ARABIC) == 123 + 0j
assert complex(' ' + ARABIC + ' ') == 123 + 0j
assert complex('\t' + ARABIC + 'j\n') == 123j
assert complex(NBSP + ARABIC + 'j') == 123j
assert complex('(' + ARABIC + ')') == 123 + 0j
assert complex(ARABIC + '+\u0664\u0665j') == 123 + 45j
assert complex(ARABIC + '-\u0664\u0665j') == 123 - 45j
assert complex('(' + ARABIC + '+\u0664\u0665j)') == 123 + 45j
assert complex(ARABIC + '+j') == 123 + 1j
assert complex('\u0661\u0662\u0663\u0664j') == 1234j
assert complex('\u0661\u0662\u0663.\u0665+\u0664\u0665j') == 123.5 + 45j
assert complex('\u0661\u0662\u0663.\u0664\u0665j') == 123.45j
assert complex(BOLD + 'j') == 123j

# --- complex(): the underscore pass validates the transformed digits ---
assert complex('\u0661\u0662\u0663_\u0664\u0665\u0666j') == 123456j
assert complex('\u0661_\u0662j') == 12j

# --- complex(): ASCII forms and failures are unchanged ---
assert complex('j') == 1j
assert complex('1.5') == 1.5 + 0j
assert complex(2 + 3j) == 2 + 3j
expect_error('complex(nd letter)', ValueError, 'complex() arg is a malformed string',
             lambda: complex(ARABIC + 'x'))
expect_error('complex(nd space nd)', ValueError, 'complex() arg is a malformed string',
             lambda: complex('\u0661 \u0662'))
expect_error('complex(_1j)', ValueError,
             "could not convert string to complex: '_1j'", lambda: complex('_1j'))
expect_error('complex(1_j)', ValueError,
             "could not convert string to complex: '1_j'", lambda: complex('1_j'))


class MyStr(str):
    pass


# --- the transform applies to str subclasses too ---
assert int(MyStr(ARABIC)) == 123
assert float(MyStr(ARABIC)) == 123.0
assert complex(MyStr(ARABIC + 'j')) == 123j
assert int(MyStr(ARABIC), 16) == 291

print("test_unicode_digit_conversion passed")
