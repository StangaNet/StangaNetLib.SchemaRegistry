using System.Text.Json;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.SchemaRegistry.Abstractions;
using StangaNetLib.SchemaRegistry.Models;

namespace StangaNetLib.SchemaRegistry.Manifests;

/// <summary>Portable export/import helpers for registry schema definitions.</summary>
public static class SchemaRegistrySnapshot
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Exports all subjects/versions currently in <paramref name="registry"/> as a JSON array
    /// of <see cref="SchemaDefinitionSnapshot"/>.
    /// </summary>
    public static async Task<Result<string>> ExportAsync(
        ISchemaRegistry registry,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(registry, nameof(registry));

        var subjects = await registry.ListSubjectsAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<SchemaDefinitionSnapshot>();

        foreach (var subject in subjects)
        {
            var versions = await registry.GetVersionsAsync(subject, cancellationToken).ConfigureAwait(false);
            if (versions.IsFailure)
                return Result<string>.Failure(versions.Error);

            foreach (var version in versions.Value!)
            {
                var schema = await registry.GetAsync(subject, version, cancellationToken).ConfigureAwait(false);
                if (schema.IsFailure)
                    return Result<string>.Failure(schema.Error);

                var def = schema.Value!;
                items.Add(new SchemaDefinitionSnapshot(
                    def.Subject,
                    def.Version,
                    def.JsonSchemaContent,
                    def.RegisteredAt));
            }
        }

        return Result<string>.Success(JsonSerializer.Serialize(items, JsonOptions));
    }

    /// <summary>
    /// Imports schemas from a JSON snapshot produced by <see cref="ExportAsync"/>.
    /// Existing subject/version pairs are skipped (no overwrite).
    /// </summary>
    public static async Task<Result> ImportAsync(
        ISchemaRegistry registry,
        string snapshotJson,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(registry, nameof(registry));
        Guard.Against.NullOrWhiteSpace(snapshotJson, nameof(snapshotJson));

        List<SchemaDefinitionSnapshot>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<SchemaDefinitionSnapshot>>(snapshotJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return Result.Failure(Error.Validation("snapshotJson", $"Invalid snapshot JSON: {ex.Message}"));
        }

        if (items is null)
            return Result.Failure(Error.Validation("snapshotJson", "Snapshot JSON deserialized to null."));

        foreach (var item in items)
        {
            if (await registry.ExistsAsync(item.Subject, item.Version, cancellationToken).ConfigureAwait(false))
                continue;

            var result = await registry.RegisterAsync(
                item.Subject,
                item.Version,
                item.JsonSchemaContent,
                cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
                return Result.Failure(result.Error);
        }

        return Result.Success();
    }
}

/// <summary>DTO used by <see cref="SchemaRegistrySnapshot"/> serialization.</summary>
public sealed record SchemaDefinitionSnapshot(
    string Subject,
    string Version,
    string JsonSchemaContent,
    DateTimeOffset RegisteredAt);
