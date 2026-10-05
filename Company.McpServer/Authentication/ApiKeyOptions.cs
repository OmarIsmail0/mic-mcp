namespace Company.McpServer.Authentication;

/// <summary>
/// Credentials that MCP clients must present to this server.
/// Unrelated to the credentials the server uses to call the external API.
/// </summary>
public sealed class ApiKeyOptions
{
    /// <summary>
    /// Expected value of the <c>X-API-Key</c> header. Set it through the
    /// <c>McpAuthentication__ApiKey</c> environment variable (or user secrets) outside development.
    /// While empty, every request to the MCP endpoint is rejected.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
