namespace StangaNetLib.SchemaRegistry.Manifests;

/// <summary>Portable backup/export manifest (aligned with embedded JSON Schema v1.0.0).</summary>
public sealed class BackupManifest
{
    /// <summary>Manifest schema version (e.g. <c>1.0.0</c>).</summary>
    public string SchemaVersion { get; set; } = BackupManifestSubjects.Version100;

    /// <summary>UTC export timestamp.</summary>
    public DateTimeOffset ExportedAt { get; set; }

    /// <summary>Product or application name that produced the export.</summary>
    public string Product { get; set; } = string.Empty;

    /// <summary>Optional overall content hash (hex / base64).</summary>
    public string? ContentHash { get; set; }

    /// <summary>Exported items.</summary>
    public List<BackupManifestItem> Items { get; set; } = [];

    /// <summary>Optional encryption envelope metadata for the archive.</summary>
    public BackupManifestEncryption? Encryption { get; set; }
}

/// <summary>Single item in a <see cref="BackupManifest"/>.</summary>
public sealed class BackupManifestItem
{
    /// <summary>Logical type (e.g. <c>blob</c>, <c>record</c>, <c>schema</c>).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Stable identifier within the export.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Relative path inside the archive, when applicable.</summary>
    public string? Path { get; set; }

    /// <summary>External payload reference, when the item is not embedded.</summary>
    public string? PayloadRef { get; set; }

    /// <summary>Checksum of the item payload.</summary>
    public string? Checksum { get; set; }

    /// <summary>Size in bytes, when known.</summary>
    public long? SizeBytes { get; set; }
}

/// <summary>Optional encryption metadata for a backup archive.</summary>
public sealed class BackupManifestEncryption
{
    /// <summary>Algorithm identifier (e.g. <c>AES-256-GCM</c>).</summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>KDF identifier (e.g. <c>PBKDF2-SHA256</c>).</summary>
    public string? Kdf { get; set; }
}
