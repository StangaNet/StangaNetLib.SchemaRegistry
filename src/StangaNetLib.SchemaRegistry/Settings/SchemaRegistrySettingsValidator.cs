using StangaNetLib.Core.Validators;

namespace StangaNetLib.SchemaRegistry.Settings;

/// <summary>
/// Validates <see cref="SchemaRegistrySettings"/> via the <see cref="Microsoft.Extensions.Options.IValidateOptions{T}"/> pipeline,
/// ensuring <see cref="SchemaRegistrySettings.BaseUrl"/> is provided when the HTTP backend is selected.
/// </summary>
internal sealed class SchemaRegistrySettingsValidator : SettingsValidatorBase<SchemaRegistrySettings>
{
    protected override void ValidateCore(SchemaRegistrySettings options, List<string> errors)
    {
        if (options.Provider == SchemaRegistryProvider.Http && string.IsNullOrWhiteSpace(options.BaseUrl))
            errors.Add("SchemaRegistrySettings.BaseUrl is required when Provider is Http.");
    }
}
