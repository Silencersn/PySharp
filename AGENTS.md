# PySharp Agent Guidelines

## Project Documentation

- Consult the [project overview and repository structure](./README.md) first.
- Use the [reference documentation index](./docs/reference/README.md) as the primary entry point for technical documentation.
- Prefer documented behavior over source-code investigation. Inspect the implementation only when the documentation cannot answer the question or appears to be outdated.

## Source Generators First

- PySharp depends heavily on Roslyn source generators: much of the type machinery, slots, method and module registration, exception factories, and test wrappers is generated rather than handwritten.
- Before exploring an implementation, read the relevant documentation under `./docs/reference/`, especially [source generators](./docs/reference/internals/source-generators.md), [architecture](./docs/reference/internals/architecture.md), and the applicable contributor guide.
- If `grep`, `rg`, or another search tool cannot find a source definition, first consider whether that definition is generated. Trace the triggering attributes or handwritten metadata and the responsible generator before concluding that the definition is missing.

## Build Requirements

- The project must build with zero warnings.
- When compiling specifically to inspect warnings, use the `--no-incremental` option.

## Generated Source

- Source generators emit their output to disk by default (`EmitCompilerGeneratedFiles` is set in `Directory.Build.props`). The generated files live under each project's `obj/<configuration>/<tfm>/generated/` and are refreshed by an ordinary build — inspect them there instead of concluding a definition is missing.

## Commit Messages

- Before creating or suggesting a commit, read `.agents\skills\git-commit-msg-guidelines\SKILL.md` and follow its requirements.