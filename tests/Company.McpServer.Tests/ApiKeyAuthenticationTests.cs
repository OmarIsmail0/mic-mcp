using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Company.McpServer.Tests;

public sealed class ApiKeyAuthenticationTests
{
    private const string InitializeRequest = """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tests","version":"1.0.0"}}}
        """;

    private const string ListToolsRequest = """
        {"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
        """;

    [Fact]
    public async Task Health_WithoutApiKey_ReturnsHealthy()
    {
        using var factory = new McpServerFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Healthy", body?["status"]);
    }

    [Fact]
    public async Task Mcp_WithoutApiKey_ReturnsUnauthorized()
    {
        using var factory = new McpServerFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(McpPost(InitializeRequest, apiKey: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mcp_WithWrongApiKey_ReturnsUnauthorized()
    {
        using var factory = new McpServerFactory();
        using var client = factory.CreateClient();

        var response = await client.SendAsync(McpPost(InitializeRequest, apiKey: "wrong-key"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mcp_WhenServerKeyIsNotConfigured_RejectsEvenAnEmptyKey()
    {
        using var factory = new McpServerFactory(serverApiKey: "");
        using var client = factory.CreateClient();

        var withEmptyKey = await client.SendAsync(McpPost(InitializeRequest, apiKey: ""));
        var withSomeKey = await client.SendAsync(McpPost(InitializeRequest, apiKey: McpServerFactory.TestApiKey));

        Assert.Equal(HttpStatusCode.Unauthorized, withEmptyKey.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withSomeKey.StatusCode);
    }

    [Fact]
    public async Task Mcp_WithValidApiKey_InitializesAndListsAllTools()
    {
        using var factory = new McpServerFactory();
        using var client = factory.CreateClient();

        var initialize = await client.SendAsync(McpPost(InitializeRequest, McpServerFactory.TestApiKey));
        var initializeBody = await initialize.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
        Assert.Contains("Company MCP Server", initializeBody);

        var listTools = await client.SendAsync(McpPost(ListToolsRequest, McpServerFactory.TestApiKey));
        var toolsBody = await listTools.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, listTools.StatusCode);
        foreach (var tool in new[]
                 {
                     "get_customer", "search_customers", "create_customer",
                     "get_product", "search_products",
                     "get_order", "get_customer_orders", "create_order"
                 })
        {
            Assert.Contains($"\"{tool}\"", toolsBody);
        }
    }

    private static HttpRequestMessage McpPost(string json, string? apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (apiKey is not null)
        {
            request.Headers.Add("X-API-Key", apiKey);
        }

        return request;
    }
}
