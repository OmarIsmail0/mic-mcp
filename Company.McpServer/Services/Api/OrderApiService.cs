using System.Net.Http.Json;
using Company.McpServer.Models.Requests;
using Company.McpServer.Models.Responses;
using Company.McpServer.Services.Interfaces;

namespace Company.McpServer.Services.Api;

public sealed class OrderApiService(HttpClient httpClient, ILogger<OrderApiService> logger)
    : IOrderApiService
{
    public async Task<OrderResponse?> GetOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        logger.LogDebug("External API: GET orders/{OrderId}", orderId);
        return await httpClient.GetFromJsonOrNullAsync<OrderResponse>($"orders/{orderId}", cancellationToken);
    }

    public async Task<IReadOnlyList<OrderResponse>> GetCustomerOrdersAsync(
        int customerId, CancellationToken cancellationToken)
    {
        logger.LogDebug("External API: GET customers/{CustomerId}/orders", customerId);
        var orders = await httpClient.GetFromJsonAsync<List<OrderResponse>>(
            $"customers/{customerId}/orders", cancellationToken);
        return orders ?? [];
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("External API: POST orders");
        using var response = await httpClient.PostAsJsonAsync("orders", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OrderResponse>(cancellationToken)
            ?? throw new HttpRequestException("The external API returned an empty response body.");
    }
}
