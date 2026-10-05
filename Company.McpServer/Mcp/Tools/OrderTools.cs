using System.ComponentModel;
using Company.McpServer.Models.Requests;
using Company.McpServer.Models.Responses;
using Company.McpServer.Services.Interfaces;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Company.McpServer.Mcp.Tools;

[McpServerToolType]
public sealed class OrderTools(IOrderApiService orderApi, ILogger<OrderTools> logger)
{
    private const int MaxLinesPerOrder = 100;
    private const int MaxQuantityPerLine = 1000;

    [McpServerTool(Name = "get_order", ReadOnly = true, Idempotent = true)]
    [Description("Retrieves a single order by id, returning its id, customer id, total amount, status and creation time. " +
                 "Use this when the order id is known; use get_customer_orders to list a customer's orders.")]
    public Task<OrderResponse> GetOrder(
        [Description("Numeric id of the order to retrieve.")] int orderId,
        CancellationToken cancellationToken) =>
        ToolGuard.RunAsync(logger, "get_order", async () =>
        {
            ToolGuard.RequireId(orderId, nameof(orderId));
            return await orderApi.GetOrderAsync(orderId, cancellationToken)
                ?? throw new McpException($"Order {orderId} was not found.");
        }, cancellationToken);

    [McpServerTool(Name = "get_customer_orders", ReadOnly = true, Idempotent = true)]
    [Description("Lists all orders placed by a given customer. Returns an empty list when the customer has no orders. " +
                 "Use this to answer questions about a customer's order history.")]
    public Task<IReadOnlyList<OrderResponse>> GetCustomerOrders(
        [Description("Numeric id of the customer whose orders should be listed.")] int customerId,
        CancellationToken cancellationToken) =>
        ToolGuard.RunAsync(logger, "get_customer_orders", async () =>
        {
            ToolGuard.RequireId(customerId, nameof(customerId));
            return await orderApi.GetCustomerOrdersAsync(customerId, cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "create_order", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("Creates a new order for a customer and returns the created order including its id, total and status. " +
                 "This changes data: only call it when the user explicitly asks to place an order. " +
                 "Look up the customer id with search_customers and the product ids with search_products first.")]
    public Task<OrderResponse> CreateOrder(
        [Description("Numeric id of the customer placing the order.")] int customerId,
        [Description("Order lines: each has a productId and a quantity (at least 1). At least one line is required.")]
        IReadOnlyList<CreateOrderLine> lines,
        CancellationToken cancellationToken) =>
        ToolGuard.RunAsync(logger, "create_order", async () =>
        {
            ToolGuard.RequireId(customerId, nameof(customerId));
            ValidateLines(lines);

            var request = new CreateOrderRequest { CustomerId = customerId, Lines = lines };
            return await orderApi.CreateOrderAsync(request, cancellationToken);
        }, cancellationToken, uncertainOutcomeHint: "call get_customer_orders to check whether the order was created.");

    private static void ValidateLines(IReadOnlyList<CreateOrderLine>? lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new McpException("'lines' must contain at least one order line.");
        }

        if (lines.Count > MaxLinesPerOrder)
        {
            throw new McpException($"'lines' can contain at most {MaxLinesPerOrder} order lines.");
        }

        foreach (var line in lines)
        {
            if (line is null)
            {
                throw new McpException("'lines' cannot contain empty entries.");
            }

            ToolGuard.RequireId(line.ProductId, "lines[].productId");
            if (line.Quantity is < 1 or > MaxQuantityPerLine)
            {
                throw new McpException($"'lines[].quantity' must be between 1 and {MaxQuantityPerLine}.");
            }
        }
    }
}
