# StangaNetLib.SchemaRegistry

[![.NET CI](https://github.com/StangaNet/StangaNetLib.SchemaRegistry/actions/workflows/main.yml/badge.svg)](https://github.com/StangaNet/StangaNetLib.SchemaRegistry/actions/workflows/main.yml)
![NuGet](https://img.shields.io/badge/nuget-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4)

JSON payload validation against versioned schemas with pluggable in-memory and HTTP registry backends. Uses `Result` / `Guard` from [`StangaNetLib.Core`](https://github.com/StangaNet/StangaNetLib.Core); optional HTTP resilience via [`StangaNetLib.Resilience`](https://github.com/StangaNet/StangaNetLib.Resilience) (`IResilientExecutor`).

## Installation

The package is hosted on **GitHub Packages** (pulls Core/Resilience from the same feed).

```xml
<!-- NuGet.config — source key must match your packageSourceCredentials -->
<configuration>
  <packageSources>
    <add key="StangaNet" value="https://nuget.pkg.github.com/StangaNet/index.json" />
  </packageSources>
</configuration>
```

```xml
<!-- .csproj -->
<PackageReference Include="StangaNetLib.SchemaRegistry" Version="1.0.0" />
```

Requires `StangaNetLib.Resilience` **1.0.0** (not 1.0.1). See [CHANGELOG.md](./CHANGELOG.md).

## Core Components

| Namespace | Components | Purpose |
|---|---|---|
| `StangaNetLib.SchemaRegistry.Abstractions` | `ISchemaRegistry`, `ISchemaValidator` | Contracts |
| `StangaNetLib.SchemaRegistry.Models` | `SchemaDefinition`, `SchemaValidationResult` | Domain models |
| `StangaNetLib.SchemaRegistry.Settings` | `SchemaRegistrySettings`, `SchemaRegistryProvider` | Options |
| `StangaNetLib.SchemaRegistry.Extensions` | `AddStangaNetLibSchemaRegistry` | DI |

## Quick Start

```csharp
builder.Services.AddStangaNetLibSchemaRegistry(builder.Configuration);
// Optional: builder.Services.AddStangaNetLibResilience(...) before HTTP provider
```

```json
{
  "SchemaRegistrySettings": {
    "Provider": "InMemory"
  }
}
```

## License

Apache-2.0 — see [LICENSE](./LICENSE) and [NOTICE](./NOTICE).
