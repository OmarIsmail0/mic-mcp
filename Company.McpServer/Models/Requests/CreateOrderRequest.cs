using System.ComponentModel;

namespace Company.McpServer.Models.Requests;

public sealed record CreateOrderRequest
{
    [Description("Id of the customer placing the order.")]
    public required int CustomerId { get; init; }

    [Description("Products to order. Must contain at least one line.")]
    public required IReadOnlyList<CreateOrderLine> Lines { get; init; }
}

public sealed record CreateOrderLine
{
    [Description("Id of the product to order.")]
    public required int ProductId { get; init; }

    [Description("Number of units to order. Must be at least 1.")]
    public required int Quantity { get; init; }
}
