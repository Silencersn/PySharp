"""Failing member of the import cycle family: raises on first execution.

:kind: helper
"""

import import_cycle_counter

import_cycle_counter.bump()
raise ValueError("boom")
