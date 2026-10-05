using Company.McpServer.Models.Requests;
using Company.McpServer.Models.Responses;

namespace Company.McpServer.Services.Interfaces;

public interface ICustomerApiService
{
    Task<CustomerResponse?> GetCustomerAsync(int customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerResponse>> SearchCustomersAsync(string query, CancellationToken cancellationToken);

    Task<CustomerResponse> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken);
}
