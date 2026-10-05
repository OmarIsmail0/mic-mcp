namespace Company.McpServer.Models.Responses;

public sealed record ProductResponse(int Id, string Name, string? Description, decimal Price);
