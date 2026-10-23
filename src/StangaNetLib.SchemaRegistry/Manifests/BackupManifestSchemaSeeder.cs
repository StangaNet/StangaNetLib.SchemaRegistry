using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.SchemaRegistry.Abstractions;

namespace StangaNetLib.SchemaRegistry.Manifests;

/// <summary>Seeds well-known backup manifest schemas into an <see cref="ISchemaRegistry"/>.</summary>
public static class BackupManifestSchemaSeeder
{
    /// <summary>
    /// Registers the embedded <c>stanganet.backup.manifest</c> / <c>1.0.0</c> JSON Schema
    /// when it is not already present.
    /// </summary>
    public static async Task<Result> SeedAsync(
        ISchemaRegistry registry,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(registry, nameof(registry));

        if (await registry.ExistsAsync(
                BackupManifestSubjects.BackupManifest,
                BackupManifestSubjects.Version100,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        var json = LoadEmbeddedSchema();
        var registered = await registry.RegisterAsync(
            BackupManifestSubjects.BackupManifest,
            BackupManifestSubjects.Version100,
            json,
            cancellationToken).ConfigureAwait(false);
        return registered.IsSuccess
            ? Result.Success()
            : Result.Failure(registered.Error);
    }

    private static string LoadEmbeddedSchema()
    {
        var assembly = typeof(BackupManifestSchemaSeeder).Assembly;
        const string resourceName = "StangaNetLib.SchemaRegistry.Manifests.backup-manifest-1.0.0.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
