namespace Company.McpServer.Models.Responses;

public sealed record OrderResponse(
    int Id,
    int CustomerId,
    decimal TotalAmount,
    string Status,
    DateTimeOffset CreatedAt);
