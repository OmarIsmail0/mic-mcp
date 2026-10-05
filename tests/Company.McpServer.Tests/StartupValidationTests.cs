using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Company.McpServer.Tests;

public sealed class StartupValidationTests
{
    [Fact]
    public void Startup_InProduction_WithoutMcpApiKey_Fails()
    {
        using var factory = ProductionFactory(("ExternalApi:BaseUrl", "https://api.test/"));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("McpAuthentication:ApiKey", exception.ToString());
    }

    [Fact]
    public void Startup_InProduction_WithoutExternalBaseUrl_Fails()
    {
        using var factory = ProductionFactory(("McpAuthentication:ApiKey", "secret"));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ExternalApi:BaseUrl", exception.ToString());
    }

    [Fact]
    public void Startup_InProduction_WithPlainHttpBaseUrl_Fails()
    {
        using var factory = ProductionFactory(
            ("McpAuthentication:ApiKey", "secret"), ("ExternalApi:BaseUrl", "http://api.test/"));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void Startup_WithInvalidApiKeyHeaderName_Fails()
    {
        using var factory = ProductionFactory(
            ("McpAuthentication:ApiKey", "secret"),
            ("ExternalApi:BaseUrl", "https://api.test/"),
            ("ExternalApi:ApiKeyHeaderName", "not a header"));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ApiKeyHeaderName", exception.ToString());
    }

    [Fact]
    public void Startup_InProduction_WithValidConfiguration_Succeeds()
    {
        using var factory = ProductionFactory(
            ("McpAuthentication:ApiKey", "secret"), ("ExternalApi:BaseUrl", "https://api.test/"));

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    private static WebApplicationFactory<Program> ProductionFactory(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value))));
        });
}
