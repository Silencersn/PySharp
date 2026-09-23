import import_cycle_moda

B_VAL = "b"
# moda has executed only its import statement so far: the module object is
# visible, the names its body assigns afterwards are not.
SAW_A = hasattr(import_cycle_moda, "A_VAL")
