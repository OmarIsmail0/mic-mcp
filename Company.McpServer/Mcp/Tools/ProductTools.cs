using System.ComponentModel;
using Company.McpServer.Models.Responses;
using Company.McpServer.Services.Interfaces;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Company.McpServer.Mcp.Tools;

[McpServerToolType]
public sealed class ProductTools(IProductApiService productApi, ILogger<ProductTools> logger)
{
    [McpServerTool(Name = "get_product", ReadOnly = true, Idempotent = true)]
    [Description("Retrieves a single product by id, returning its id, name, description and price. " +
                 "Use this when the product id is already known; use search_products to look a product up by name.")]
    public Task<ProductResponse> GetProduct(
        [Description("Numeric id of the product to retrieve.")] int productId,
        CancellationToken cancellationToken) =>
        ToolGuard.RunAsync(logger, "get_product", async () =>
        {
            ToolGuard.RequireId(productId, nameof(productId));
            return await productApi.GetProductAsync(productId, cancellationToken)
                ?? throw new McpException($"Product {productId} was not found.");
        }, cancellationToken);

    [McpServerTool(Name = "search_products", ReadOnly = true, Idempotent = true)]
    [Description("Searches the product catalog by free text (name or description) and returns the matching products " +
                 "with their prices. Returns an empty list when nothing matches. Use this to find product ids before ordering.")]
    public Task<IReadOnlyList<ProductResponse>> SearchProducts(
        [Description("Text to search for, such as part of a product name or description.")] string query,
        CancellationToken cancellationToken) =>
        ToolGuard.RunAsync(logger, "search_products", async () =>
        {
            var text = ToolGuard.RequireText(query, nameof(query));
            return await productApi.SearchProductsAsync(text, cancellationToken);
        }, cancellationToken);
}
