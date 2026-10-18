using FluentAssertions;
using StangaNetLib.Core.Common;
using StangaNetLib.SchemaRegistry.Implementations;

namespace StangaNetLib.SchemaRegistry.Tests;

public class InMemorySchemaRegistryTests
{
    private const string Subject = "user-created";
    private const string V1 = "v1";
    private const string V2 = "v2";

    private const string SimpleSchema = """
        {
          "type": "object",
          "properties": {
            "name": { "type": "string" }
          },
          "required": ["name"]
        }
        """;

    private static InMemorySchemaRegistry Create() => new();

    [Fact]
    public async Task RegisterAsync_NewSchema_ReturnsSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        var result = await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be(Subject);
        result.Value.Version.Should().Be(V1);
        result.Value.JsonSchemaContent.Should().Be(SimpleSchema);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateVersion_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        var result = await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task RegisterAsync_SameSubjectDifferentVersion_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        var result = await registry.RegisterAsync(Subject, V2, SimpleSchema, ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be(V2);
    }

    [Fact]
    public async Task GetAsync_ExistingSchema_ReturnsDefinition()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        var result = await registry.GetAsync(Subject, V1, ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be(Subject);
        result.Value.Version.Should().Be(V1);
    }

    [Fact]
    public async Task GetAsync_NonExistingSubject_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        var result = await registry.GetAsync("non-existing", V1, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetAsync_NonExistingVersion_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        var result = await registry.GetAsync(Subject, "v99", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetLatestAsync_MultipleVersions_ReturnsMostRecentlyRegistered()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);
        await Task.Delay(2, ct);
        await registry.RegisterAsync(Subject, V2, SimpleSchema, ct);

        var result = await registry.GetLatestAsync(Subject, ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be(V2);
    }

    [Fact]
    public async Task GetLatestAsync_SingleVersion_ReturnsThatVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        var result = await registry.GetLatestAsync(Subject, ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be(V1);
    }

    [Fact]
    public async Task GetLatestAsync_NoVersions_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        var result = await registry.GetLatestAsync(Subject, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetVersionsAsync_ExistingSubject_ReturnsAllVersions()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);
        await registry.RegisterAsync(Subject, V2, SimpleSchema, ct);

        var result = await registry.GetVersionsAsync(Subject, ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().Contain(V1).And.Contain(V2);
    }

    [Fact]
    public async Task GetVersionsAsync_NonExistingSubject_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        var result = await registry.GetVersionsAsync("non-existing", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_ExistingSchema_RemovesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        var result = await registry.DeleteAsync(Subject, V1, ct);

        result.IsSuccess.Should().BeTrue();
        var exists = await registry.ExistsAsync(Subject, V1, ct);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingSchema_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        var result = await registry.DeleteAsync(Subject, V1, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_OneVersionOfMultiple_LeavesOtherVersionsIntact()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);
        await registry.RegisterAsync(Subject, V2, SimpleSchema, ct);

        await registry.DeleteAsync(Subject, V1, ct);

        var v1Exists = await registry.ExistsAsync(Subject, V1, ct);
        var v2Exists = await registry.ExistsAsync(Subject, V2, ct);
        v1Exists.Should().BeFalse();
        v2Exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ExistingSchema_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, V1, SimpleSchema, ct);

        var exists = await registry.ExistsAsync(Subject, V1, ct);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingSchema_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        var exists = await registry.ExistsAsync(Subject, V1, ct);

        exists.Should().BeFalse();
    }

    // --- RegisterAsync schema validation ---

    [Fact]
    public async Task RegisterAsync_InvalidSchemaContent_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        var result = await registry.RegisterAsync(Subject, V1, "not-valid-json", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task RegisterAsync_ValidJsonButBoolSchema_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        // JSON Schema allows `true` and `false` as valid schemas
        var result = await registry.RegisterAsync(Subject, V1, "true", ct);

        result.IsSuccess.Should().BeTrue();
    }

    // --- ListSubjectsAsync ---

    [Fact]
    public async Task ListSubjectsAsync_NoSubjects_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();

        var subjects = await registry.ListSubjectsAsync(ct);

        subjects.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSubjectsAsync_MultipleSubjects_ReturnsAllSubjects()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync("order-placed", V1, SimpleSchema, ct);
        await registry.RegisterAsync("user-created", V1, SimpleSchema, ct);

        var subjects = await registry.ListSubjectsAsync(ct);

        subjects.Should().Contain("order-placed").And.Contain("user-created");
    }

    // --- GetVersionsAsync registration order ---

    [Fact]
    public async Task GetVersionsAsync_ReturnsVersionsInRegistrationOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Create();
        await registry.RegisterAsync(Subject, "v3", SimpleSchema, ct);
        await registry.RegisterAsync(Subject, "v1", SimpleSchema, ct);
        await registry.RegisterAsync(Subject, "v2", SimpleSchema, ct);

        var result = await registry.GetVersionsAsync(Subject, ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainInOrder("v3", "v1", "v2");
    }
}
