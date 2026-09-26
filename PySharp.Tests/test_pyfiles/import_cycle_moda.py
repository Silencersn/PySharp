"""Cycle partner of import_cycle_modb for module-level circular imports.

:kind: helper
"""

import import_cycle_modb

A_VAL = "a"
SAW_B = import_cycle_modb.B_VAL
