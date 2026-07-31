# PySharp

English | [中文](./docs/readme/README.zh-CN.md)

> A Python 3 interpreter implemented from scratch in C#, targeting .NET 10.

PySharp is a pure C# implementation of a Python interpreter. It takes Python source code through a
full compilation pipeline — **lexer → parser (AST) → semantic analysis → bytecode compiler →
stack-based virtual machine** — and executes it entirely on the .NET runtime, with no dependency on
CPython or any Python runtime. The core library is trimming-friendly and AOT-compatible and can be
embedded in your application.

---

## Features

- **Complete compilation pipeline** — lexing, AST, semantic analysis, bytecode compilation, and a
  custom stack-based virtual machine.
- **Rich built-in object model** — `int` (arbitrary precision), `float`, `complex`, `str`,
  `bytes`/`bytearray`, `list`, `tuple`, `dict`, `set`/`frozenset`, `memoryview`, and more, each
  implemented as a `Py*Object` / `Py*ObjectType` pair.
- **Modern language features** — closures, generators, coroutines and `async`/`await`, comprehensions,
  decorators, classes and metaclasses, descriptors, `match`/`case`, exception groups, f-strings,
  generics and type vars, and more.
- **Embedded standard-library modules** — `builtins`, `math`, `operator`, `time`, `random`,
  `threading`, `queue`, `typing`, `string`, and more, plus a virtual file system backing `open()`
  and imports.
- **Embeddable host API** — run Python files, code strings, or a REPL, with control over I/O, search
  paths, and program arguments.
- **Built-in tooling** — Roslyn analyzers and source generators that auto-generate type machinery and
  keep the code style consistent.
- **Modern .NET** — targets `net10.0`, `IsTrimmable` and `IsAotCompatible`.

---

## Quick start

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet build PySharp.slnx   # build
dotnet test  PySharp.slnx   # run tests
```

Embed in your application:

```csharp
using PySharp.Runtime;

PyInterpreter.RunFile("script.py");          // run a Python file
PyInterpreter.RunCode("print('hello')");     // run a code string
PyInterpreter.RunRepl();                     // interactive REPL
```

For more usage (custom environments, virtual file system, architecture details), see the test corpus
in `PySharp.Tests/` and the source.

---

## Repository layout

| Path | Description |
| --- | --- |
| `PySharp/` | Core interpreter library: `Compilation/` (lexer/AST/bytecode), `Runtime/` (VM and environments), `Modules/` (built-in objects and stdlib). |
| `PySharp.SourceGeneration/`, `PySharp.SourceGeneration.Internal/` | Roslyn source generators (`[PyType]`, `[PyException]`, etc.). |
| `PySharp.Analyzer/`, `PySharp.Analyzer.Internal/` | Roslyn analyzers (public `PYSP*` and internal `PYSPI*` rules). |
| `PySharp.Tests/` | MSTest suite; `test_pyfiles/` holds the Python test corpus. |
| `PySharp.slnx` | Solution file. |

---

Copyright © 2025–2026 Silencersn. All rights reserved.

---

English | [中文](./docs/readme/README.zh-CN.md)
