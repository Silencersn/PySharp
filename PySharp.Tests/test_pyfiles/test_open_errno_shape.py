"""open() failures carry the full [Errno N] strerror shape with errno/strerror/filename set.

:kind: test
"""

# Regression: open() built its failure exceptions from a single
# pre-formatted string, so the message text looked right for some
# branches while errno/strerror/filename stayed None and args held one
# bare element. Every branch now constructs OSError the
# PyErr_SetFromErrnoWithFilenameObject way: (errno, strerror, filename),
# which OSError.__str__ renders as "[Errno N] strerror: 'filename'".
# Binary-mode seek below start shares the same shape without a filename.
# The diff runner reuses one working directory across both sides, so
# the fixture stays idempotent: the missing-probe name is never created,
# and re-creating the data probe overwrites identical content.


def show(label, f):
    try:
        r = f()
        print(label, "=", repr(r))
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)), "| errno:", getattr(e, "errno", "-"), "| strerror:", getattr(e, "strerror", "-"), "| filename:", getattr(e, "filename", "-"), "| args:", e.args)


show("open_missing", lambda: open("_pysharpprobe_missing.txt"))
show("open_missing_dir", lambda: open("_no_such_dir_xyz/f.txt"))

created = open("_pysharpprobe_data.txt", "w")
created.write("x")
created.close()

show("open_x_conflict", lambda: open("_pysharpprobe_data.txt", "x"))
show("open_ok_after", lambda: open("_pysharpprobe_data.txt").read())

show("open_dir_win", lambda: open("."))
show("open_dir_read", lambda: open(".", "r"))

binary = open("_pysharpprobe_data.txt", "rb")
show("seek_neg_bin", lambda: binary.seek(-1))
show("seek_end_bin", lambda: binary.seek(0, 2))
binary.close()

text = open("_pysharpprobe_data.txt", "r")
show("seek_neg_text", lambda: text.seek(-1))
text.close()

e = FileNotFoundError(2, "No such file or directory", "a'b")
print("handmade_str:", repr(str(e)))
print("handmade_args:", e.args, "| errno:", e.errno, "| filename:", repr(e.filename))
