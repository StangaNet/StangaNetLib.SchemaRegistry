# Changelog

All notable changes to **StangaNetLib.SchemaRegistry** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-10-18

### Added
- `ISchemaRegistry` / `ISchemaValidator` with in-memory and HTTP-backed registries.
- JSON Schema validation via JsonSchema.Net; versioned subjects and evolution helpers.
- Optional `IResilientExecutor` integration (Resilience pipeline `"SchemaRegistry"`) for remote calls.
- `SchemaRegistrySettings` with options validation; DI via `AddStangaNetLibSchemaRegistry`.
- Dual targeting `net8.0` / `net9.0`, XML docs, symbols (`snupkg`), package icon, and MSBuild consumer metadata stamp.
- Depends on `StangaNetLib.Core` 1.0.0 and `StangaNetLib.Resilience` 1.0.0; Microsoft.Extensions.* pinned to 8.x.
- CI via shared StangaNet reusable workflow (push / PR / manual; publish outside PRs).

[Unreleased]: https://github.com/StangaNet/StangaNetLib.SchemaRegistry/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/StangaNet/StangaNetLib.SchemaRegistry/releases/tag/v1.0.0
