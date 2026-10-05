namespace Company.McpServer.Authentication;

public static class ApiKeyConstants
{
    /// <summary>Header clients must send to authenticate against the MCP server.</summary>
    public const string HeaderName = "X-API-Key";

    /// <summary>Configuration section holding <see cref="ApiKeyOptions"/>.</summary>
    public const string ConfigurationSection = "McpAuthentication";

    /// <summary>Path of the MCP endpoint. Only this path (and below) requires the API key.</summary>
    public const string ProtectedPath = "/mcp";
}
