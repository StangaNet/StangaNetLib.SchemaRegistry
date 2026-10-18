using System.Collections.Concurrent;
using Json.Schema;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.SchemaRegistry.Abstractions;
using StangaNetLib.SchemaRegistry.Models;

namespace StangaNetLib.SchemaRegistry.Implementations;

/// <summary>
/// In-process, in-memory <see cref="ISchemaRegistry"/> implementation backed by
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
/// <remarks>
/// Each subject maintains a per-version dictionary keyed by version string (case-insensitive).
/// A monotonically increasing global sequence number is assigned to every registration via
/// <c>Interlocked.Increment</c> so that <see cref="GetLatestAsync"/> returns a
/// deterministic result even when multiple versions share identical <c>RegisteredAt</c> timestamps.
/// Suitable for unit testing and single-instance applications; state is not shared across instances.
/// </remarks>
internal sealed class InMemorySchemaRegistry : ISchemaRegistry
{
    // (SequenceNumber, SchemaDefinition) — sequence enables deterministic ordering within a subject.
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, (long Seq, SchemaDefinition Def)>> _store =
        new(StringComparer.OrdinalIgnoreCase);

    private long _globalSeq;

    public Task<Result<SchemaDefinition>> RegisterAsync(
        string subject, string version, string jsonSchemaContent, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));
        Guard.Against.NullOrWhiteSpace(jsonSchemaContent, nameof(jsonSchemaContent));

        try
        {
            JsonSchema.FromText(jsonSchemaContent);
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<SchemaDefinition>.Failure(
                Error.Validation("SchemaRegistry.InvalidSchema",
                    $"Schema content is not a valid JSON Schema: {ex.Message}")));
        }

        var versions = _store.GetOrAdd(
            subject,
            _ => new ConcurrentDictionary<string, (long, SchemaDefinition)>(StringComparer.OrdinalIgnoreCase));

        var definition = new SchemaDefinition(subject, version, jsonSchemaContent);
        var seq = Interlocked.Increment(ref _globalSeq);

        if (!versions.TryAdd(version, (seq, definition)))
            return Task.FromResult(Result<SchemaDefinition>.Failure(
                Error.Conflict("SchemaRegistry.DuplicateVersion",
                    $"Schema '{subject}' version '{version}' already exists.")));

        return Task.FromResult(Result<SchemaDefinition>.Success(definition));
    }

    public Task<Result<SchemaDefinition>> GetAsync(
        string subject, string version, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));

        if (_store.TryGetValue(subject, out var versions) && versions.TryGetValue(version, out var entry))
            return Task.FromResult(Result<SchemaDefinition>.Success(entry.Def));

        return Task.FromResult(Result<SchemaDefinition>.Failure(
            Error.NotFound("Schema", $"{subject}:{version}")));
    }

    public Task<Result<SchemaDefinition>> GetLatestAsync(
        string subject, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));

        if (!_store.TryGetValue(subject, out var versions) || versions.IsEmpty)
            return Task.FromResult(Result<SchemaDefinition>.Failure(Error.NotFound("Schema", subject)));

        // Use sequence number as tiebreaker so the result is deterministic even when RegisteredAt timestamps collide.
        var latest = versions.Values.MaxBy(e => e.Seq)!.Def;
        return Task.FromResult(Result<SchemaDefinition>.Success(latest));
    }

    public Task<Result<IReadOnlyList<string>>> GetVersionsAsync(
        string subject, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));

        if (!_store.TryGetValue(subject, out var versions) || versions.IsEmpty)
            return Task.FromResult(Result<IReadOnlyList<string>>.Failure(Error.NotFound("Schema", subject)));

        IReadOnlyList<string> result = versions
            .OrderBy(kv => kv.Value.Seq)
            .Select(kv => kv.Key)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(Result<IReadOnlyList<string>>.Success(result));
    }

    public Task<Result> DeleteAsync(
        string subject, string version, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));

        if (!_store.TryGetValue(subject, out var versions) || !versions.TryRemove(version, out _))
            return Task.FromResult(Result.Failure(Error.NotFound("Schema", $"{subject}:{version}")));

        return Task.FromResult(Result.Success());
    }

    public Task<bool> ExistsAsync(
        string subject, string version, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));

        var exists = _store.TryGetValue(subject, out var versions) && versions.ContainsKey(version);
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<string>> ListSubjectsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> subjects = _store.Keys.ToList().AsReadOnly();
        return Task.FromResult(subjects);
    }
}
