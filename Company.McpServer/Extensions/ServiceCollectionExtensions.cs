using Company.McpServer.Authentication;
using Company.McpServer.Configuration;
using Company.McpServer.Services.Api;
using Company.McpServer.Services.Interfaces;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace Company.McpServer.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers configuration, the external API clients and the MCP server (Streamable HTTP, tools
    /// discovered from this assembly).
    /// </summary>
    public static IServiceCollection AddMcpApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddConfiguration(configuration);
        services.AddExternalApiClients();
        services.AddMcpServerWithTools(configuration.GetSection(McpSettings.SectionName).Get<McpSettings>() ?? new());
        return services;
    }

    private static void AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Outside Development a missing MCP key stops the app at startup instead of silently serving 401s.
        // (In Development, and always at request time, an empty key still makes the middleware reject requests.)
        services.AddOptions<ApiKeyOptions>()
            .Bind(configuration.GetSection(ApiKeyConstants.ConfigurationSection))
            .Validate<IHostEnvironment>(
                (options, environment) => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(options.ApiKey),
                $"'{ApiKeyConstants.ConfigurationSection}:ApiKey' must be set (environment variable " +
                $"'{ApiKeyConstants.ConfigurationSection}__ApiKey').")
            .ValidateOnStart();

        services.AddOptions<McpSettings>()
            .Bind(configuration.GetSection(McpSettings.SectionName));

        services.AddOptions<ApiSettings>()
            .Bind(configuration.GetSection(ApiSettings.SectionName))
            .Validate<IHostEnvironment>(
                (settings, environment) => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var uri)
                                           && (uri.Scheme == Uri.UriSchemeHttps
                                               || (uri.Scheme == Uri.UriSchemeHttp && environment.IsDevelopment())),
                $"'{ApiSettings.SectionName}:BaseUrl' must be set to an absolute https URL " +
                "(http is only allowed in Development).")
            .Validate(
                settings => IsValidHeaderName(settings.ApiKeyHeaderName),
                $"'{ApiSettings.SectionName}:ApiKeyHeaderName' must be a valid HTTP header name.")
            .Validate(
                settings => settings.TimeoutSeconds > 0,
                $"'{ApiSettings.SectionName}:TimeoutSeconds' must be greater than zero.")
            .ValidateOnStart();
    }

    private static bool IsValidHeaderName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && new HttpRequestMessage().Headers.TryAddWithoutValidation(name, "test");

    private static void AddExternalApiClients(this IServiceCollection services)
    {
        services.AddExternalApiClient<ICustomerApiService, CustomerApiService>();
        services.AddExternalApiClient<IProductApiService, ProductApiService>();
        services.AddExternalApiClient<IOrderApiService, OrderApiService>();
    }

    private static void AddExternalApiClient<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<ApiSettings>>().Value;

            // A trailing slash keeps any path in the base URL when relative URIs are combined with it.
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                client.DefaultRequestHeaders.Add(settings.ApiKeyHeaderName, settings.ApiKey);
            }
        });
    }

    private static void AddMcpServerWithTools(this IServiceCollection services, McpSettings settings)
    {
        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = settings.ServerName,
                    Version = settings.ServerVersion
                };
                options.ServerInstructions = settings.Instructions;
            })
            // Stateless: no per-client session, so the server scales without session affinity.
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly();
    }
}
