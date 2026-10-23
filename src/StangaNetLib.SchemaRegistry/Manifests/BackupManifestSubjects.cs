namespace StangaNetLib.SchemaRegistry.Manifests;

/// <summary>Well-known subject / version identifiers for portable backup manifests.</summary>
public static class BackupManifestSubjects
{
    /// <summary>Subject for the portable backup/export manifest schema.</summary>
    public const string BackupManifest = "stanganet.backup.manifest";

    /// <summary>Initial schema version for <see cref="BackupManifest"/>.</summary>
    public const string Version100 = "1.0.0";
}
