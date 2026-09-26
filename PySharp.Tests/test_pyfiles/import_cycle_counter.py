"""Re-import counter shared by the import cycle fixtures.

:kind: helper
"""

COUNT = 0


def bump():
    global COUNT
    COUNT += 1
    return COUNT
