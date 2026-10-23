# StangaNetLib.SchemaRegistry — Copilot instructions

- Versioned JSON Schema registry (in-memory + HTTP) and payload validation via JsonSchema.Net.
- Prefer `Result` / `Result<T>` and `Guard` from `StangaNetLib.Core`.
- Optional resilience: `IResilientExecutor` from `StangaNetLib.Resilience.Execution` (pipeline `"SchemaRegistry"`).
- Keep public APIs documented (`///`) and options-driven via `SchemaRegistrySettings`.
- DI: `AddStangaNetLibSchemaRegistry` / HTTP-specific overloads.
- Pin `StangaNetLib.Resilience` 1.0.0 (not 1.0.1); never reintroduce ME 10.x.
- Match existing naming, file-scoped namespaces, and EditorConfig / Roslyn conventions.
- Tests use xUnit v3 + FluentAssertions; cover `net8.0`, `net9.0`, and `net10.0` when changing shared behavior.
