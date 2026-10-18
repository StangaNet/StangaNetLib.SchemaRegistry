namespace StangaNetLib.SchemaRegistry.Settings;

/// <summary>
/// Configuration settings for StangaNetLib.SchemaRegistry.
/// Bind from the <c>"SchemaRegistrySettings"</c> configuration section.
/// </summary>
public sealed class SchemaRegistrySettings
{
    /// <summary>Configuration section name used for binding.</summary>
    public const string SectionName = "SchemaRegistrySettings";

    /// <summary>
    /// Schema registry backend. Default: <see cref="SchemaRegistryProvider.InMemory"/>.
    /// </summary>
    public SchemaRegistryProvider Provider { get; set; } = SchemaRegistryProvider.InMemory;

    /// <summary>
    /// Base URL of the remote schema registry.
    /// Required when <see cref="Provider"/> is <see cref="SchemaRegistryProvider.Http"/>.
    /// Example: <c>https://schema-registry.example.com</c>
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>HTTP client timeout for remote registry calls. Default: 30 seconds.</summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        if (Provider == SchemaRegistryProvider.Http && string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException(
                "SchemaRegistrySettings.BaseUrl is required when Provider is Http.");
    }
}

/// <summary>Schema registry backend provider.</summary>
public enum SchemaRegistryProvider
{
    /// <summary>In-process, in-memory registry. Suitable for testing and single-instance scenarios.</summary>
    InMemory,

    /// <summary>Remote HTTP-based registry. Requires <see cref="SchemaRegistrySettings.BaseUrl"/>.</summary>
    Http
}
