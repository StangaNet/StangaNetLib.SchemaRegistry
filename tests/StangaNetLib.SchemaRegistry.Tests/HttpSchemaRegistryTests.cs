using System.Net;
using System.Text.Json;
using FluentAssertions;
using StangaNetLib.Core.Common;
using StangaNetLib.SchemaRegistry.Implementations;

namespace StangaNetLib.SchemaRegistry.Tests;

public sealed class HttpSchemaRegistryTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responses.Dequeue());
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private const string SimpleSchema =
        """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""";

    private static (HttpSchemaRegistry sut, FakeHttpMessageHandler handler) Create()
    {
        var handler = new FakeHttpMessageHandler();
        var client  = new HttpClient(handler) { BaseAddress = new Uri("http://schema-registry.test/") };
        return (new HttpSchemaRegistry(client), handler);
    }

    private static HttpResponseMessage JsonResponse(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                System.Text.Encoding.UTF8,
                "application/json")
        };

    private static object SchemaDto(string subject = "order-placed", string version = "v1") =>
        new { subject, version, schema = SimpleSchema, registeredAt = DateTimeOffset.UtcNow };

    // ── RegisterAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_RemoteSuccess_ReturnsDefinition()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(JsonResponse(SchemaDto(), HttpStatusCode.Created));

        var result = await sut.RegisterAsync("order-placed", "v1", SimpleSchema, ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("order-placed");
        result.Value.Version.Should().Be("v1");
    }

    [Fact]
    public async Task RegisterAsync_RemoteConflict_ReturnsConflictError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.Conflict));

        var result = await sut.RegisterAsync("order-placed", "v1", SimpleSchema, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task RegisterAsync_RemoteBadRequest_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var result = await sut.RegisterAsync("order-placed", "v1", "not-a-schema", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task RegisterAsync_RemoteServerError_ReturnsInternalError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sut.RegisterAsync("order-placed", "v1", SimpleSchema, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Internal);
    }

    // ── GetAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_RemoteSuccess_ReturnsDefinition()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(JsonResponse(SchemaDto()));

        var result = await sut.GetAsync("order-placed", "v1", ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("order-placed");
        result.Value.Version.Should().Be("v1");
    }

    [Fact]
    public async Task GetAsync_RemoteNotFound_ReturnsNotFoundError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetAsync("order-placed", "v99", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetAsync_RemoteServerError_ReturnsInternalError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sut.GetAsync("order-placed", "v1", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Internal);
    }

    // ── GetLatestAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestAsync_RemoteSuccess_ReturnsLatestDefinition()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(JsonResponse(SchemaDto(version: "v2")));

        var result = await sut.GetLatestAsync("order-placed", ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be("v2");
    }

    [Fact]
    public async Task GetLatestAsync_RemoteNotFound_ReturnsNotFoundError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetLatestAsync("unknown-subject", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── GetVersionsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetVersionsAsync_RemoteSuccess_ReturnsList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(JsonResponse(new[] { "v1", "v2" }));

        var result = await sut.GetVersionsAsync("order-placed", ct);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().Contain("v1").And.Contain("v2");
    }

    [Fact]
    public async Task GetVersionsAsync_RemoteNotFound_ReturnsNotFoundError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.GetVersionsAsync("unknown-subject", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemoteSuccess_ReturnsSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await sut.DeleteAsync("order-placed", "v1", ct);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_RemoteNotFound_ReturnsNotFoundError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.DeleteAsync("order-placed", "v99", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ── ExistsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_RemoteHead200_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK));

        var exists = await sut.ExistsAsync("order-placed", "v1", ct);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_RemoteHead404_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NotFound));

        var exists = await sut.ExistsAsync("order-placed", "v99", ct);

        exists.Should().BeFalse();
    }

    // ── ListSubjectsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ListSubjectsAsync_RemoteSuccess_ReturnsList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(JsonResponse(new[] { "order-placed", "user-created" }));

        var subjects = await sut.ListSubjectsAsync(ct);

        subjects.Should().Contain("order-placed").And.Contain("user-created");
    }

    [Fact]
    public async Task ListSubjectsAsync_RemoteServerError_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var (sut, handler) = Create();
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var subjects = await sut.ListSubjectsAsync(ct);

        subjects.Should().BeEmpty();
    }
}
