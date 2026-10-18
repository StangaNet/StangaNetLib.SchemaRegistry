namespace StangaNetLib.SchemaRegistry.Models;

/// <summary>
/// Represents the outcome of validating a JSON payload against a schema.
/// </summary>
public sealed class SchemaValidationResult
{
    /// <summary>True when the payload conforms to the schema.</summary>
    public bool IsValid { get; }

    /// <summary>Validation error messages. Empty when <see cref="IsValid"/> is true.</summary>
    public IReadOnlyList<string> Errors { get; }

    private SchemaValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    /// <summary>Creates a successful validation result with no errors.</summary>
    public static SchemaValidationResult Valid() => new(true, []);

    /// <summary>Creates a failed validation result with the provided error messages.</summary>
    /// <param name="errors">Validation error messages describing each constraint violation.</param>
    /// <returns>A <see cref="SchemaValidationResult"/> where <see cref="IsValid"/> is <c>false</c>.</returns>
    public static SchemaValidationResult Invalid(IReadOnlyList<string> errors) => new(false, errors);
}
