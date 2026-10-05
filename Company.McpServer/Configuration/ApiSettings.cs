namespace Company.McpServer.Configuration;

/// <summary>
/// Connection settings for the external/internal REST API that the MCP tools call.
/// These credentials are separate from the key MCP clients use to reach this server.
/// </summary>
public sealed class ApiSettings
{
    public const string SectionName = "ExternalApi";

    /// <summary>Absolute base URL of the REST API, e.g. <c>https://api.example.com/</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Key sent to the REST API. Set it through the <c>ExternalApi__ApiKey</c> environment variable.
    /// When empty, no authentication header is sent.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Name of the header that carries <see cref="ApiKey"/>.</summary>
    public string ApiKeyHeaderName { get; set; } = "X-API-Key";

    /// <summary>Per-request timeout for calls to the REST API.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
