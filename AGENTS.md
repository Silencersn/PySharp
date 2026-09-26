# PySharp Agent Guidelines

## Project Documentation

- Consult the [project overview and repository structure](./README.md) first.
- Use the [reference documentation index](./docs/reference/README.md) as the primary entry point for technical documentation.
- Prefer documented behavior over source-code investigation. Inspect the implementation only when the documentation cannot answer the question or appears to be outdated.

## Build Requirements

- The project must build with zero warnings.
- When compiling specifically to inspect warnings, use the `--no-incremental` option.

## Generated Source

- Before inspecting source-generator output, rebuild the project with `EmitCompilerGeneratedFiles` enabled. This ensures that generated files are present and up to date.