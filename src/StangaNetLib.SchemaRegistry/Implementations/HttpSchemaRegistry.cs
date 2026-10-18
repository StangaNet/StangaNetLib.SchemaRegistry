using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StangaNetLib.Core.Common;
using StangaNetLib.Core.Guards;
using StangaNetLib.Resilience.Execution;
using StangaNetLib.SchemaRegistry.Abstractions;
using StangaNetLib.SchemaRegistry.Models;

namespace StangaNetLib.SchemaRegistry.Implementations;

/// <summary>
/// HTTP-backed <see cref="ISchemaRegistry"/> implementation that delegates all operations to a
/// remote schema registry over HTTP. Uses an <see cref="HttpClient"/> configured via the
/// <c>StangaNetLibSchemaRegistry</c> named client.
/// </summary>
/// <remarks>
/// When an <see cref="IResilientExecutor"/> is registered in the DI container it is injected
/// automatically and used to wrap all HTTP calls to the remote registry (retry, circuit-breaker,
/// and timeout are driven by the <c>"SchemaRegistry"</c> pipeline in
/// <c>ResilienceSettings.Pipelines</c>).
/// When no executor is present the library falls back to direct <see cref="HttpClient"/> calls.
/// <para>
/// Inside resilience lambdas, all non-success HTTP responses throw
/// <c>SchemaRegistryHttpException</c> so Polly can observe the failure and apply its policies.
/// To avoid retrying non-retriable status codes (404, 409, 400), configure the
/// <c>"SchemaRegistry"</c> pipeline's <c>ShouldHandle</c> predicate to exclude
/// <c>SchemaRegistryHttpException</c> with 4xx status codes.
/// </para>
/// <para>
/// Alternatively, attach a Polly delegating handler via the <c>configureHttpClient</c> callback
/// in <c>AddStangaNetLibSchemaRegistry</c> / <c>AddStangaNetLibHttpSchemaRegistry</c> instead of
/// registering an <see cref="IResilientExecutor"/>; do not combine both approaches on the same
/// instance.
/// </para>
/// </remarks>
internal sealed class HttpSchemaRegistry : ISchemaRegistry
{
    /// <summary>Resilience pipeline name consumed from <c>ResilienceSettings.Pipelines</c> when <see cref="IResilientExecutor"/> is active.</summary>
    internal const string PipelineName = "SchemaRegistry";

    private readonly HttpClient           _httpClient;
    private readonly IResilientExecutor?   _resilience;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpSchemaRegistry(HttpClient httpClient, IResilientExecutor? resilience = null)
    {
        _httpClient = Guard.Against.Null(httpClient, nameof(httpClient));
        _resilience = resilience;
    }

    public async Task<Result<SchemaDefinition>> RegisterAsync(
        string subject, string version, string jsonSchemaContent, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));
        Guard.Against.NullOrWhiteSpace(jsonSchemaContent, nameof(jsonSchemaContent));

        var body = new { version, schema = jsonSchemaContent };

        if (_resilience is not null)
            return await _resilience.ExecuteAsync<SchemaDefinition>(PipelineName, async innerCt =>
            {
                var response = await _httpClient.PostAsJsonAsync(
                    $"schemas/{Uri.EscapeDataString(subject)}/versions", body, JsonOptions, innerCt);
                EnsureSuccess(response);
                var dto = await response.Content.ReadFromJsonAsync<SchemaDefinitionDto>(JsonOptions, innerCt);
                return dto is null
                    ? throw new InvalidOperationException("Remote registry returned an empty response.")
                    : dto.ToSchemaDefinition();
            }, cancellationToken);

        var resp = await _httpClient.PostAsJsonAsync(
            $"schemas/{Uri.EscapeDataString(subject)}/versions", body, JsonOptions, cancellationToken);

        if (resp.StatusCode == HttpStatusCode.Conflict)
            return Error.Conflict("SchemaRegistry.DuplicateVersion",
                $"Schema '{subject}' version '{version}' already exists.");

        if (resp.StatusCode == HttpStatusCode.BadRequest)
            return Error.Validation("SchemaRegistry.InvalidSchema",
                "Remote registry rejected the schema as invalid.");

        if (!resp.IsSuccessStatusCode)
            return Error.Internal($"Remote registry returned {(int)resp.StatusCode}.");

        var result = await resp.Content.ReadFromJsonAsync<SchemaDefinitionDto>(JsonOptions, cancellationToken);
        return result is null
            ? Error.Internal("Remote registry returned an empty response.")
            : Result<SchemaDefinition>.Success(result.ToSchemaDefinition());
    }

    public async Task<Result<SchemaDefinition>> GetAsync(
        string subject, string version, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));

        var url = $"schemas/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}";

        if (_resilience is not null)
            return await _resilience.ExecuteAsync<SchemaDefinition>(PipelineName, async innerCt =>
            {
                var response = await _httpClient.GetAsync(url, innerCt);
                EnsureSuccess(response);
                var dto = await response.Content.ReadFromJsonAsync<SchemaDefinitionDto>(JsonOptions, innerCt);
                return dto is null
                    ? throw new InvalidOperationException("Remote registry returned an empty response.")
                    : dto.ToSchemaDefinition();
            }, cancellationToken);

        var resp = await _httpClient.GetAsync(url, cancellationToken);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return Error.NotFound("Schema", $"{subject}:{version}");

        if (!resp.IsSuccessStatusCode)
            return Error.Internal($"Remote registry returned {(int)resp.StatusCode}.");

        var result = await resp.Content.ReadFromJsonAsync<SchemaDefinitionDto>(JsonOptions, cancellationToken);
        return result is null
            ? Error.Internal("Remote registry returned an empty response.")
            : Result<SchemaDefinition>.Success(result.ToSchemaDefinition());
    }

    public async Task<Result<SchemaDefinition>> GetLatestAsync(
        string subject, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));

        var url = $"schemas/{Uri.EscapeDataString(subject)}/versions/latest";

        if (_resilience is not null)
            return await _resilience.ExecuteAsync<SchemaDefinition>(PipelineName, async innerCt =>
            {
                var response = await _httpClient.GetAsync(url, innerCt);
                EnsureSuccess(response);
                var dto = await response.Content.ReadFromJsonAsync<SchemaDefinitionDto>(JsonOptions, innerCt);
                return dto is null
                    ? throw new InvalidOperationException("Remote registry returned an empty response.")
                    : dto.ToSchemaDefinition();
            }, cancellationToken);

        var resp = await _httpClient.GetAsync(url, cancellationToken);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return Error.NotFound("Schema", subject);

        if (!resp.IsSuccessStatusCode)
            return Error.Internal($"Remote registry returned {(int)resp.StatusCode}.");

        var result = await resp.Content.ReadFromJsonAsync<SchemaDefinitionDto>(JsonOptions, cancellationToken);
        return result is null
            ? Error.Internal("Remote registry returned an empty response.")
            : Result<SchemaDefinition>.Success(result.ToSchemaDefinition());
    }

    public async Task<Result<IReadOnlyList<string>>> GetVersionsAsync(
        string subject, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));

        var url = $"schemas/{Uri.EscapeDataString(subject)}/versions";

        if (_resilience is not null)
            return await _resilience.ExecuteAsync<IReadOnlyList<string>>(PipelineName, async innerCt =>
            {
                var response = await _httpClient.GetAsync(url, innerCt);
                EnsureSuccess(response);
                var versions = await response.Content.ReadFromJsonAsync<string[]>(JsonOptions, innerCt);
                return (IReadOnlyList<string>)(versions?.ToList() ?? []);
            }, cancellationToken);

        var resp = await _httpClient.GetAsync(url, cancellationToken);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return Error.NotFound("Schema", subject);

        if (!resp.IsSuccessStatusCode)
            return Error.Internal($"Remote registry returned {(int)resp.StatusCode}.");

        var result = await resp.Content.ReadFromJsonAsync<string[]>(JsonOptions, cancellationToken);
        IReadOnlyList<string> list = result?.ToList() ?? [];
        return Result<IReadOnlyList<string>>.Success(list);
    }

    public async Task<Result> DeleteAsync(
        string subject, string version, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));

        var url = $"schemas/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}";

        if (_resilience is not null)
            return await _resilience.ExecuteAsync(PipelineName, async innerCt =>
            {
                var response = await _httpClient.DeleteAsync(url, innerCt);
                EnsureSuccess(response);
            }, cancellationToken);

        var resp = await _httpClient.DeleteAsync(url, cancellationToken);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return Error.NotFound("Schema", $"{subject}:{version}");

        if (!resp.IsSuccessStatusCode)
            return Error.Internal($"Remote registry returned {(int)resp.StatusCode}.");

        return Result.Success();
    }

    public async Task<bool> ExistsAsync(
        string subject, string version, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(subject, nameof(subject));
        Guard.Against.NullOrWhiteSpace(version, nameof(version));

        if (_resilience is not null)
        {
            var r = await _resilience.ExecuteAsync<bool>(PipelineName, async innerCt =>
            {
                var request = new HttpRequestMessage(HttpMethod.Head,
                    $"schemas/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}");
                var response = await _httpClient.SendAsync(request, innerCt);
                return response.IsSuccessStatusCode;
            }, cancellationToken);
            return r.IsSuccess && r.Value;
        }

        var req = new HttpRequestMessage(HttpMethod.Head,
            $"schemas/{Uri.EscapeDataString(subject)}/versions/{Uri.EscapeDataString(version)}");
        var resp = await _httpClient.SendAsync(req, cancellationToken);
        return resp.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<string>> ListSubjectsAsync(CancellationToken cancellationToken = default)
    {
        if (_resilience is not null)
        {
            var r = await _resilience.ExecuteAsync<IReadOnlyList<string>>(PipelineName, async innerCt =>
            {
                var response = await _httpClient.GetAsync("schemas", innerCt);
                if (!response.IsSuccessStatusCode) return [];
                var subjects = await response.Content.ReadFromJsonAsync<string[]>(JsonOptions, innerCt);
                return (IReadOnlyList<string>)(subjects?.ToList() ?? []);
            }, cancellationToken);
            return r.IsSuccess ? r.Value! : [];
        }

        var resp = await _httpClient.GetAsync("schemas", cancellationToken);

        if (!resp.IsSuccessStatusCode)
            return [];

        var result = await resp.Content.ReadFromJsonAsync<string[]>(JsonOptions, cancellationToken);
        return result?.ToList() ?? [];
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Throws <see cref="SchemaRegistryHttpException"/> for any non-success response so Polly can
    /// observe the failure. Configure the <c>"SchemaRegistry"</c> pipeline's
    /// <c>ShouldHandle</c> predicate to exclude 4xx status codes if retry is not desired for them.
    /// </summary>
    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new SchemaRegistryHttpException(response.StatusCode);
    }

    private sealed record SchemaDefinitionDto(
        string Subject,
        string Version,
        string Schema,
        DateTimeOffset RegisteredAt)
    {
        public SchemaDefinition ToSchemaDefinition() =>
            new(Subject, Version, Schema, RegisteredAt);
    }

    /// <summary>
    /// Thrown inside resilience lambdas so Polly can observe non-success HTTP responses.
    /// Carries the <see cref="HttpStatusCode"/> for fine-grained pipeline configuration.
    /// </summary>
    internal sealed class SchemaRegistryHttpException(HttpStatusCode statusCode) : Exception
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }
}
