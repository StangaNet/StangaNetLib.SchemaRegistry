using System.Text.Json;
using FluentAssertions;
using StangaNetLib.SchemaRegistry.Extensions;
using StangaNetLib.SchemaRegistry.Implementations;
using StangaNetLib.SchemaRegistry.Manifests;
using Microsoft.Extensions.DependencyInjection;

namespace StangaNetLib.SchemaRegistry.Tests;

public sealed class BackupManifestTests
{
    [Fact]
    public async Task Seed_And_Validate_Manifest()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddStangaNetLibInMemorySchemaRegistry();
        await using var sp = services.BuildServiceProvider();
        await sp.AddStangaNetLibBackupManifestSchemasAsync(ct);

        var validator = sp.GetRequiredService<StangaNetLib.SchemaRegistry.Abstractions.ISchemaValidator>();
        var payload = JsonSerializer.Serialize(new BackupManifest
        {
            SchemaVersion = BackupManifestSubjects.Version100,
            ExportedAt = DateTimeOffset.UtcNow,
            Product = "TestApp",
            Items =
            [
                new BackupManifestItem { Type = "blob", Id = "1", Path = "a.bin", SizeBytes = 10 }
            ],
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var result = await validator.ValidateAsync(
            BackupManifestSubjects.BackupManifest,
            BackupManifestSubjects.Version100,
            payload,
            ct);
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Snapshot_ExportImport_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var source = new InMemorySchemaRegistry();
        (await source.RegisterAsync("demo", "1", """{"type":"object"}""", ct)).IsSuccess.Should().BeTrue();

        var json = await SchemaRegistrySnapshot.ExportAsync(source, ct);
        json.IsSuccess.Should().BeTrue();

        var dest = new InMemorySchemaRegistry();
        (await SchemaRegistrySnapshot.ImportAsync(dest, json.Value!, ct)).IsSuccess.Should().BeTrue();
        (await dest.ExistsAsync("demo", "1", ct)).Should().BeTrue();
    }
}
