using Company.McpServer.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Company.McpServer.Tests;

/// <summary>Hosts the real application in-memory with a known MCP API key.</summary>
public sealed class McpServerFactory(string? serverApiKey = McpServerFactory.TestApiKey) : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-api-key";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
            services.PostConfigure<ApiKeyOptions>(options => options.ApiKey = serverApiKey ?? string.Empty));
}
