using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StangaNetLib.Resilience.Execution;
using StangaNetLib.SchemaRegistry.Abstractions;
using StangaNetLib.SchemaRegistry.Implementations;
using StangaNetLib.SchemaRegistry.Settings;

namespace StangaNetLib.SchemaRegistry.Extensions;

/// <summary>
/// Extension methods for registering StangaNetLib.SchemaRegistry services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "StangaNetLibSchemaRegistry";

    /// <summary>
    /// Registers <see cref="ISchemaRegistry"/> and <see cref="ISchemaValidator"/> based on the
    /// <c>"SchemaRegistrySettings"</c> configuration section.
    /// </summary>
    /// <example>
    /// appsettings.json (in-memory):
    /// <code>
    /// {
    ///   "SchemaRegistrySettings": {
    ///     "Provider": "InMemory"
    ///   }
    /// }
    /// </code>
    /// appsettings.json (HTTP):
    /// <code>
    /// {
    ///   "SchemaRegistrySettings": {
    ///     "Provider": "Http",
    ///     "BaseUrl": "https://schema-registry.example.com",
    ///     "HttpTimeout": "00:00:30"
    ///   }
    /// }
    /// </code>
    /// Program.cs:
    /// <code>
    /// builder.Services.AddStangaNetLibSchemaRegistry(builder.Configuration);
    /// </code>
    /// </example>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="configureHttpClient">
    /// Optional callback invoked on the <see cref="IHttpClientBuilder"/> when the <c>Http</c> provider is
    /// selected. Ignored for the <c>InMemory</c> provider. Use it to attach delegating handlers to the
    /// registry's <see cref="System.Net.Http.HttpClient"/>.
    /// Do not combine this with a registered <see cref="IResilientExecutor"/>; use one approach or the other.
    /// </param>
    /// <remarks>
    /// When <see cref="IResilientExecutor"/> is registered in the DI container before this call (via
    /// <c>AddStangaNetLibResilience</c>) and the <c>Http</c> provider is selected, it is injected
    /// automatically into <see cref="HttpSchemaRegistry"/> and used to wrap all HTTP calls with the
    /// <c>"SchemaRegistry"</c> resilience pipeline.
    /// </remarks>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibSchemaRegistry(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        services.AddOptions<SchemaRegistrySettings>()
            .Bind(configuration.GetSection(SchemaRegistrySettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SchemaRegistrySettings>, SchemaRegistrySettingsValidator>();

        var settings = configuration
            .GetSection(SchemaRegistrySettings.SectionName)
            .Get<SchemaRegistrySettings>() ?? new SchemaRegistrySettings();

        settings.Validate();
        services.AddSingleton(settings);

        return settings.Provider == SchemaRegistryProvider.Http
            ? services.AddHttpRegistryCore(settings, configureHttpClient)
            : services.AddInMemoryRegistryCore();
    }

    /// <summary>
    /// Registers <see cref="ISchemaRegistry"/> and <see cref="ISchemaValidator"/> with an in-process,
    /// in-memory backend. Suitable for testing and single-instance applications.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibInMemorySchemaRegistry(this IServiceCollection services)
    {
        var settings = new SchemaRegistrySettings { Provider = SchemaRegistryProvider.InMemory };
        services.AddSingleton(settings);
        services.AddSingleton(Options.Create(settings));
        services.AddSingleton<IValidateOptions<SchemaRegistrySettings>, SchemaRegistrySettingsValidator>();
        return services.AddInMemoryRegistryCore();
    }

    /// <summary>
    /// Registers <see cref="ISchemaRegistry"/> and <see cref="ISchemaValidator"/> with an HTTP backend
    /// pointing to a remote registry.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="baseUrl">Base URL of the remote schema registry (e.g. <c>https://schema-registry.example.com</c>).</param>
    /// <param name="timeout">HTTP request timeout. Defaults to 30 seconds.</param>
    /// <param name="configureHttpClient">
    /// Optional callback to further configure the <see cref="IHttpClientBuilder"/> for the registry
    /// <see cref="System.Net.Http.HttpClient"/>. Use it to attach delegating handlers.
    /// Do not combine this with a registered <see cref="IResilientExecutor"/>; use one approach or the other.
    /// </param>
    /// <remarks>
    /// When <see cref="IResilientExecutor"/> is registered in the DI container before this call (via
    /// <c>AddStangaNetLibResilience</c>), it is injected automatically into <see cref="HttpSchemaRegistry"/>
    /// and used to wrap all HTTP calls with the <c>"SchemaRegistry"</c> resilience pipeline.
    /// </remarks>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddStangaNetLibHttpSchemaRegistry(
        this IServiceCollection services,
        string baseUrl,
        TimeSpan? timeout = null,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        var settings = new SchemaRegistrySettings
        {
            Provider = SchemaRegistryProvider.Http,
            BaseUrl = baseUrl,
            HttpTimeout = timeout ?? TimeSpan.FromSeconds(30)
        };

        settings.Validate();
        services.AddSingleton(settings);
        services.AddSingleton(Options.Create(settings));
        services.AddSingleton<IValidateOptions<SchemaRegistrySettings>, SchemaRegistrySettingsValidator>();
        return services.AddHttpRegistryCore(settings, configureHttpClient);
    }

    private static IServiceCollection AddInMemoryRegistryCore(this IServiceCollection services)
    {
        services.AddSingleton<ISchemaRegistry, InMemorySchemaRegistry>();
        services.AddSingleton<ISchemaValidator, SchemaValidator>();
        return services;
    }

    private static IServiceCollection AddHttpRegistryCore(
        this IServiceCollection services,
        SchemaRegistrySettings settings,
        Action<IHttpClientBuilder>? configureHttpClient = null)
    {
        var builder = services.AddHttpClient(HttpClientName, client =>
        {
            client.BaseAddress = new Uri(settings.BaseUrl!.TrimEnd('/') + "/");
            client.Timeout = settings.HttpTimeout;
        });
        configureHttpClient?.Invoke(builder);

        services.AddSingleton<ISchemaRegistry>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new HttpSchemaRegistry(
                factory.CreateClient(HttpClientName),
                sp.GetService<IResilientExecutor>());
        });

        services.AddSingleton<ISchemaValidator, SchemaValidator>();
        return services;
    }
}
