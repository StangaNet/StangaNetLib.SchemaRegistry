using StangaNetLib.Core.Guards;

namespace StangaNetLib.SchemaRegistry.Models;

/// <summary>
/// Represents a versioned JSON Schema stored in the registry.
/// </summary>
public sealed class SchemaDefinition
{
    /// <summary>Logical name grouping related schema versions (e.g. <c>order-placed</c>, <c>user-created</c>).</summary>
    public string Subject { get; }

    /// <summary>Version identifier within the subject (e.g. <c>v1</c>, <c>2.0.0</c>).</summary>
    public string Version { get; }

    /// <summary>Raw JSON Schema content conforming to any JSON Schema draft.</summary>
    public string JsonSchemaContent { get; }

    /// <summary>UTC timestamp when this schema version was registered.</summary>
    public DateTimeOffset RegisteredAt { get; }

    /// <summary>
    /// Initializes a new <see cref="SchemaDefinition"/>.
    /// </summary>
    /// <param name="subject">Logical subject name. Must not be null or whitespace.</param>
    /// <param name="version">Version identifier. Must not be null or whitespace.</param>
    /// <param name="jsonSchemaContent">Raw JSON Schema string. Must not be null or whitespace.</param>
    /// <param name="registeredAt">Registration timestamp. Defaults to <see cref="DateTimeOffset.UtcNow"/> when null.</param>
    public SchemaDefinition(string subject, string version, string jsonSchemaContent, DateTimeOffset? registeredAt = null)
    {
        Subject = Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Version = Guard.Against.NullOrWhiteSpace(version, nameof(version));
        JsonSchemaContent = Guard.Against.NullOrWhiteSpace(jsonSchemaContent, nameof(jsonSchemaContent));
        RegisteredAt = registeredAt ?? DateTimeOffset.UtcNow;
    }
}
