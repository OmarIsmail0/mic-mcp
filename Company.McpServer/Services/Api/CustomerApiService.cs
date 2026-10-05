using System.Net.Http.Json;
using Company.McpServer.Models.Requests;
using Company.McpServer.Models.Responses;
using Company.McpServer.Services.Interfaces;

namespace Company.McpServer.Services.Api;

public sealed class CustomerApiService(HttpClient httpClient, ILogger<CustomerApiService> logger)
    : ICustomerApiService
{
    public async Task<CustomerResponse?> GetCustomerAsync(int customerId, CancellationToken cancellationToken)
    {
        logger.LogDebug("External API: GET customers/{CustomerId}", customerId);
        return await httpClient.GetFromJsonOrNullAsync<CustomerResponse>($"customers/{customerId}", cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerResponse>> SearchCustomersAsync(
        string query, CancellationToken cancellationToken)
    {
        // The query text is not logged: it may contain personal data such as names or emails.
        logger.LogDebug("External API: GET customers?search=<redacted>");
        var customers = await httpClient.GetFromJsonAsync<List<CustomerResponse>>(
            $"customers?search={Uri.EscapeDataString(query)}", cancellationToken);
        return customers ?? [];
    }

    public async Task<CustomerResponse> CreateCustomerAsync(
        CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("External API: POST customers");
        using var response = await httpClient.PostAsJsonAsync("customers", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CustomerResponse>(cancellationToken)
            ?? throw new HttpRequestException("The external API returned an empty response body.");
    }
}
