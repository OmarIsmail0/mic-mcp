using Company.McpServer.Models.Requests;
using Company.McpServer.Models.Responses;

namespace Company.McpServer.Services.Interfaces;

public interface IOrderApiService
{
    Task<OrderResponse?> GetOrderAsync(int orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderResponse>> GetCustomerOrdersAsync(int customerId, CancellationToken cancellationToken);

    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}
