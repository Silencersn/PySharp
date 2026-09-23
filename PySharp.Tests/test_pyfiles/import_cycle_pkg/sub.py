# Imports the package that is still initializing: VALUE, assigned before the
# import in __init__, is visible; nothing defined after it is.
import import_cycle_pkg

B = "b"
HELPER = "helper:" + str(import_cycle_pkg.VALUE)
