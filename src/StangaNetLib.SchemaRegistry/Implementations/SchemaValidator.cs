using System.Collections.Concurrent;
using System.Text.Json;
using Json.Schema;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.SchemaRegistry.Abstractions;
using StangaNetLib.SchemaRegistry.Models;

namespace StangaNetLib.SchemaRegistry.Implementations;

/// <summary>
/// Default <see cref="ISchemaValidator"/> implementation that fetches schemas from an
/// <see cref="ISchemaRegistry"/> and evaluates JSON payloads against them using
/// <c>JsonSchema.Net</c> in hierarchical output mode.
/// </summary>
/// <remarks>
/// Parsed <see cref="JsonSchema"/> instances are immutable and expensive to construct; they are
/// cached in a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by raw schema content so
/// each unique schema string is parsed only once for the lifetime of the service.
/// </remarks>
internal sealed class SchemaValidator : ISchemaValidator
{
    private readonly ISchemaRegistry _registry;

    // Parsed JsonSchema instances are immutable and expensive to create — cache them by raw content.
    private readonly ConcurrentDictionary<string, JsonSchema> _schemaCache = new();

    public SchemaValidator(ISchemaRegistry registry)
    {
        _registry = Guard.Against.Null(registry, nameof(registry));
    }

    public async Task<Result<SchemaValidationResult>> ValidateAsync(
        string subject, string version, string jsonPayload, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));
        Guard.Against.NullOrWhiteSpace(jsonPayload, nameof(jsonPayload));

        var schemaResult = await _registry.GetAsync(subject, version, cancellationToken);
        if (schemaResult.IsFailure)
            return Result<SchemaValidationResult>.Failure(schemaResult.Error);

        return Validate(schemaResult.Value!, jsonPayload);
    }

    public Result<SchemaValidationResult> Validate(SchemaDefinition schema, string jsonPayload)
    {
        Guard.Against.Null(schema, nameof(schema));
        Guard.Against.NullOrWhiteSpace(jsonPayload, nameof(jsonPayload));

        try
        {
            var jsonSchema = _schemaCache.GetOrAdd(schema.JsonSchemaContent, JsonSchema.FromText);
            var element = JsonDocument.Parse(jsonPayload).RootElement;
            var results = jsonSchema.Evaluate(element, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });

            if (results.IsValid)
                return Result<SchemaValidationResult>.Success(SchemaValidationResult.Valid());

            var errors = CollectErrors(results);
            return Result<SchemaValidationResult>.Success(SchemaValidationResult.Invalid(errors));
        }
        catch (Exception ex)
        {
            return Result<SchemaValidationResult>.Failure(
                Error.Internal($"Schema validation failed: {ex.Message}"));
        }
    }

    private static List<string> CollectErrors(EvaluationResults result)
    {
        var acc = new List<string>();
        Collect(result, acc);
        return acc;
    }

    private static void Collect(EvaluationResults r, List<string> acc)
    {
        if (!r.IsValid)
        {
            if (r.Errors != null)
                foreach (var kv in r.Errors)
                    acc.Add($"[{kv.Key}] {kv.Value}");

            if (r.Details != null)
                foreach (var d in r.Details)
                    Collect(d, acc);
        }
    }
}
