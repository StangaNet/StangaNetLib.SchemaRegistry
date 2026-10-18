using StangaNetLib.Core.Common;
using StangaNetLib.SchemaRegistry.Models;

namespace StangaNetLib.SchemaRegistry.Abstractions;

/// <summary>
/// Provides CRUD operations for versioned JSON Schemas grouped by subject.
/// </summary>
public interface ISchemaRegistry
{
    /// <summary>
    /// Registers a new schema version under the given subject.
    /// </summary>
    /// <param name="subject">Logical name grouping related versions (e.g. <c>order-placed</c>).</param>
    /// <param name="version">Version identifier (e.g. <c>v1</c>, <c>2.0.0</c>). Must be unique within the subject.</param>
    /// <param name="jsonSchemaContent">Raw JSON Schema string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}"/> containing the registered <see cref="SchemaDefinition"/> on success,
    /// or <see cref="ErrorType.Conflict"/> if the version already exists,
    /// or <see cref="ErrorType.Validation"/> if <paramref name="jsonSchemaContent"/> is not a valid JSON Schema.
    /// </returns>
    Task<Result<SchemaDefinition>> RegisterAsync(
        string subject,
        string version,
        string jsonSchemaContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific schema version by subject and version identifier.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="version">The version identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}"/> containing the <see cref="SchemaDefinition"/> on success,
    /// or a <see cref="ErrorType.NotFound"/> error if the subject/version combination does not exist.
    /// </returns>
    Task<Result<SchemaDefinition>> GetAsync(
        string subject,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most recently registered schema version for a subject.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}"/> containing the latest <see cref="SchemaDefinition"/>,
    /// or a <see cref="ErrorType.NotFound"/> error if no versions exist for the subject.
    /// </returns>
    Task<Result<SchemaDefinition>> GetLatestAsync(
        string subject,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all version identifiers registered under a subject.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}"/> containing the list of version identifiers,
    /// or a <see cref="ErrorType.NotFound"/> error if the subject does not exist.
    /// </returns>
    Task<Result<IReadOnlyList<string>>> GetVersionsAsync(
        string subject,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific schema version.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="version">The version identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result.Success()"/> on success,
    /// or a <see cref="ErrorType.NotFound"/> error if the subject/version combination does not exist.
    /// </returns>
    Task<Result> DeleteAsync(
        string subject,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a specific schema version exists.
    /// </summary>
    /// <param name="subject">The subject name.</param>
    /// <param name="version">The version identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the subject/version combination exists; otherwise <c>false</c>.</returns>
    Task<bool> ExistsAsync(
        string subject,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all subjects registered in the registry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of subject names. Empty when no subjects have been registered.</returns>
    Task<IReadOnlyList<string>> ListSubjectsAsync(CancellationToken cancellationToken = default);
}
