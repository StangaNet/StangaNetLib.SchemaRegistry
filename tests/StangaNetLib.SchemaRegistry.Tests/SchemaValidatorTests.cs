using FluentAssertions;
using StangaNetLib.Core.Common;
using StangaNetLib.SchemaRegistry.Implementations;
using StangaNetLib.SchemaRegistry.Models;

namespace StangaNetLib.SchemaRegistry.Tests;

public class SchemaValidatorTests
{
    private const string Subject = "order-placed";
    private const string Version = "v1";

    // No $schema keyword: avoids JsonSchema.Net trying to fetch the meta-schema URI at evaluation time.
    private const string Schema = """
        {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "age":  { "type": "integer" }
          },
          "required": ["name"]
        }
        """;

    private static (InMemorySchemaRegistry registry, SchemaValidator validator) CreatePair()
    {
        var registry = new InMemorySchemaRegistry();
        var validator = new SchemaValidator(registry);
        return (registry, validator);
    }

    // --- ValidateAsync (registry-backed) ---

    [Fact]
    public async Task ValidateAsync_ValidPayload_ReturnsSuccessWithIsValidTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (registry, validator) = CreatePair();
        await registry.RegisterAsync(Subject, Version, Schema, ct);

        var result = await validator.ValidateAsync(Subject, Version, """{"name":"Alice","age":30}""", ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_MissingRequiredField_ReturnsSuccessWithIsValidFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (registry, validator) = CreatePair();
        await registry.RegisterAsync(Subject, Version, Schema, ct);

        var result = await validator.ValidateAsync(Subject, Version, """{"age":30}""", ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_NonExistingSchema_ReturnsFailureNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, validator) = CreatePair();

        var result = await validator.ValidateAsync(Subject, Version, """{"name":"Alice"}""", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // --- Validate (direct SchemaDefinition) ---

    [Fact]
    public void Validate_ValidPayload_ReturnsSuccessWithIsValidTrue()
    {
        var (_, validator) = CreatePair();
        var schema = new SchemaDefinition(Subject, Version, Schema);

        var result = validator.Validate(schema, """{"name":"Bob","age":25}""");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_OnlyRequiredFieldPresent_IsValid()
    {
        var (_, validator) = CreatePair();
        var schema = new SchemaDefinition(Subject, Version, Schema);

        var result = validator.Validate(schema, """{"name":"Charlie"}""");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingRequiredField_ReturnsInvalidWithErrors()
    {
        var (_, validator) = CreatePair();
        var schema = new SchemaDefinition(Subject, Version, Schema);

        var result = validator.Validate(schema, """{"age":25}""");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_WrongFieldType_ReturnsInvalidWithErrors()
    {
        var (_, validator) = CreatePair();
        var schema = new SchemaDefinition(Subject, Version, Schema);

        var result = validator.Validate(schema, """{"name":"Alice","age":"not-a-number"}""");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse();
        result.Value.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_MalformedJsonPayload_ReturnsInternalFailure()
    {
        var (_, validator) = CreatePair();
        var schema = new SchemaDefinition(Subject, Version, Schema);

        var result = validator.Validate(schema, "{ this is not json }");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Internal);
    }

    [Fact]
    public void Validate_InvalidSchemaContent_ReturnsInternalFailure()
    {
        var (_, validator) = CreatePair();
        var schema = new SchemaDefinition(Subject, Version, "not-valid-json");

        var result = validator.Validate(schema, """{"name":"Alice"}""");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Internal);
    }

    [Fact]
    public void Validate_EmptyObject_WhenNoRequired_IsValid()
    {
        var (_, validator) = CreatePair();
        const string noRequiredSchema = """{"type":"object"}""";
        var schema = new SchemaDefinition(Subject, Version, noRequiredSchema);

        var result = validator.Validate(schema, "{}");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
    }
}
