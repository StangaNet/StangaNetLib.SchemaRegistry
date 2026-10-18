using StangaNetLib.Core.Common;
using StangaNetLib.SchemaRegistry.Models;

namespace StangaNetLib.SchemaRegistry.Abstractions;

/// <summary>
/// Validates JSON payloads against schemas stored in the registry.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Fetches the schema identified by <paramref name="subject"/> and <paramref name="version"/> from the registry,
    /// then validates <paramref name="jsonPayload"/> against it.
    /// </summary>
    /// <param name="subject">The schema subject name.</param>
    /// <param name="version">The schema version identifier.</param>
    /// <param name="jsonPayload">Raw JSON string to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping a <see cref="SchemaValidationResult"/> — check
    /// <see cref="SchemaValidationResult.IsValid"/> for the validation outcome.
    /// Returns a failed <see cref="Result{T}"/> (e.g. <see cref="ErrorType.NotFound"/>) when the schema
    /// cannot be retrieved, or <see cref="ErrorType.Internal"/> on unexpected parsing failures.
    /// </returns>
    Task<Result<SchemaValidationResult>> ValidateAsync(
        string subject,
        string version,
        string jsonPayload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates <paramref name="jsonPayload"/> directly against a <see cref="SchemaDefinition"/>
    /// without a registry lookup.
    /// </summary>
    /// <param name="schema">The schema definition to validate against.</param>
    /// <param name="jsonPayload">Raw JSON string to validate.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> wrapping a <see cref="SchemaValidationResult"/>,
    /// or a failed result with <see cref="ErrorType.Internal"/> on parsing errors.
    /// </returns>
    Result<SchemaValidationResult> Validate(SchemaDefinition schema, string jsonPayload);
}
