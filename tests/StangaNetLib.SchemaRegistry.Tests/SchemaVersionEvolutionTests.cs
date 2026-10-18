using FluentAssertions;
using StangaNetLib.SchemaRegistry.Implementations;

namespace StangaNetLib.SchemaRegistry.Tests;

public sealed class SchemaVersionEvolutionTests
{
    private const string Subject = "product-event";

    // v1: requires id + name
    private const string SchemaV1 = """
        {
          "type": "object",
          "properties": {
            "id":   { "type": "string" },
            "name": { "type": "string" }
          },
          "required": ["id", "name"]
        }
        """;

    // v2 (breaking): also requires category
    private const string SchemaV2Breaking = """
        {
          "type": "object",
          "properties": {
            "id":       { "type": "string" },
            "name":     { "type": "string" },
            "category": { "type": "string" }
          },
          "required": ["id", "name", "category"]
        }
        """;

    // v3 (non-breaking): adds optional description
    private const string SchemaV3Optional = """
        {
          "type": "object",
          "properties": {
            "id":          { "type": "string" },
            "name":        { "type": "string" },
            "description": { "type": "string" }
          },
          "required": ["id", "name"]
        }
        """;

    private static (InMemorySchemaRegistry registry, SchemaValidator validator) CreatePair()
    {
        var registry  = new InMemorySchemaRegistry();
        var validator = new SchemaValidator(registry);
        return (registry, validator);
    }

    // ── Breaking change: v2 adds required field ────────────────────────────

    [Fact]
    public async Task BreakingChange_OldPayload_FailsAgainstV2()
    {
        var ct = TestContext.Current.CancellationToken;
        var (registry, validator) = CreatePair();
        await registry.RegisterAsync(Subject, "v1", SchemaV1, ct);
        await registry.RegisterAsync(Subject, "v2", SchemaV2Breaking, ct);

        // v1-compatible payload — missing required 'category'
        var result = await validator.ValidateAsync(Subject, "v2", """{"id":"1","name":"Widget"}""", ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeFalse("v2 requires 'category'");
        result.Value.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BreakingChange_OldPayload_StillValidAgainstV1()
    {
        var ct = TestContext.Current.CancellationToken;
        var (registry, validator) = CreatePair();
        await registry.RegisterAsync(Subject, "v1", SchemaV1, ct);
        await registry.RegisterAsync(Subject, "v2", SchemaV2Breaking, ct);

        var result = await validator.ValidateAsync(Subject, "v1", """{"id":"1","name":"Widget"}""", ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue("v1 does not require 'category'");
    }

    // ── Non-breaking change: v3 adds optional field ────────────────────────

    [Fact]
    public async Task NonBreakingChange_OldPayload_StillValidAgainstV3()
    {
        var ct = TestContext.Current.CancellationToken;
        var (registry, validator) = CreatePair();
        await registry.RegisterAsync(Subject, "v1", SchemaV1, ct);
        await registry.RegisterAsync(Subject, "v3", SchemaV3Optional, ct);

        // v1 payload satisfies v3 required fields (description is optional)
        var result = await validator.ValidateAsync(Subject, "v3", """{"id":"1","name":"Widget"}""", ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue("v3 only adds an optional 'description'");
    }

    // ── GetLatest tracks newest registered ─────────────────────────────────

    [Fact]
    public async Task VersionEvolution_GetLatest_ReturnsNewestRegistered()
    {
        var ct = TestContext.Current.CancellationToken;
        var (registry, _) = CreatePair();
        await registry.RegisterAsync(Subject, "v1", SchemaV1, ct);
        await registry.RegisterAsync(Subject, "v2", SchemaV2Breaking, ct);

        var latest = await registry.GetLatestAsync(Subject, ct);

        latest.IsSuccess.Should().BeTrue();
        latest.Value!.Version.Should().Be("v2");
    }

    // ── Old version remains accessible after new version registered ─────────

    [Fact]
    public async Task VersionEvolution_OldVersion_RemainsAccessible()
    {
        var ct = TestContext.Current.CancellationToken;
        var (registry, _) = CreatePair();
        await registry.RegisterAsync(Subject, "v1", SchemaV1, ct);
        await registry.RegisterAsync(Subject, "v2", SchemaV2Breaking, ct);

        var v1 = await registry.GetAsync(Subject, "v1", ct);

        v1.IsSuccess.Should().BeTrue();
        v1.Value!.Version.Should().Be("v1");
        v1.Value.JsonSchemaContent.Should().Be(SchemaV1);
    }

    // ── Deprecate old version: delete v1, GetLatest still resolves ──────────

    [Fact]
    public async Task VersionEvolution_DeleteV1_GetLatestReturnsV2()
    {
        var ct = TestContext.Current.CancellationToken;
        var (registry, _) = CreatePair();
        await registry.RegisterAsync(Subject, "v1", SchemaV1, ct);
        await registry.RegisterAsync(Subject, "v2", SchemaV2Breaking, ct);

        await registry.DeleteAsync(Subject, "v1", ct);
        var latest = await registry.GetLatestAsync(Subject, ct);

        latest.IsSuccess.Should().BeTrue();
        latest.Value!.Version.Should().Be("v2");
    }
}
