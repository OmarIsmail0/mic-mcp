using Company.McpServer.Models.Responses;

namespace Company.McpServer.Services.Interfaces;

public interface IProductApiService
{
    Task<ProductResponse?> GetProductAsync(int productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductResponse>> SearchProductsAsync(string query, CancellationToken cancellationToken);
}
