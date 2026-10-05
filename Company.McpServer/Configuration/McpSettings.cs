namespace Company.McpServer.Configuration;

/// <summary>Identity and guidance the MCP server advertises to clients during initialization.</summary>
public sealed class McpSettings
{
    public const string SectionName = "Mcp";

    public string ServerName { get; set; } = "Company MCP Server";

    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>Optional usage hints shown to the LLM client.</summary>
    public string? Instructions { get; set; }
}
