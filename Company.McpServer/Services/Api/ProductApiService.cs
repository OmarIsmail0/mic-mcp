using System.Net.Http.Json;
using Company.McpServer.Models.Responses;
using Company.McpServer.Services.Interfaces;

namespace Company.McpServer.Services.Api;

public sealed class ProductApiService(HttpClient httpClient, ILogger<ProductApiService> logger)
    : IProductApiService
{
    public async Task<ProductResponse?> GetProductAsync(int productId, CancellationToken cancellationToken)
    {
        logger.LogDebug("External API: GET products/{ProductId}", productId);
        return await httpClient.GetFromJsonOrNullAsync<ProductResponse>($"products/{productId}", cancellationToken);
    }

    public async Task<IReadOnlyList<ProductResponse>> SearchProductsAsync(
        string query, CancellationToken cancellationToken)
    {
        logger.LogDebug("External API: GET products?search=<redacted>");
        var products = await httpClient.GetFromJsonAsync<List<ProductResponse>>(
            $"products?search={Uri.EscapeDataString(query)}", cancellationToken);
        return products ?? [];
    }
}
