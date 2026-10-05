using System.Net;
using Company.McpServer.Mcp.Tools;
using Company.McpServer.Models.Requests;
using Company.McpServer.Models.Responses;
using Company.McpServer.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;

namespace Company.McpServer.Tests;

public sealed class CustomerToolsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetCustomer_WithNonPositiveId_ThrowsMcpExceptionWithoutCallingTheApi(int customerId)
    {
        var api = new FakeCustomerApi();

        await Assert.ThrowsAsync<McpException>(() => CreateTools(api).GetCustomer(customerId, CancellationToken.None));

        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task GetCustomer_WhenApiReturnsNothing_ThrowsNotFound()
    {
        var api = new FakeCustomerApi { Customer = null };

        var exception = await Assert.ThrowsAsync<McpException>(
            () => CreateTools(api).GetCustomer(42, CancellationToken.None));

        Assert.Equal("Customer 42 was not found.", exception.Message);
    }

    [Fact]
    public async Task GetCustomer_WhenApiFails_ThrowsMcpExceptionWithoutLeakingDetails()
    {
        var api = new FakeCustomerApi
        {
            Failure = new HttpRequestException("Connection refused to https://internal-host:8443/secret", null, HttpStatusCode.InternalServerError)
        };

        var exception = await Assert.ThrowsAsync<McpException>(
            () => CreateTools(api).GetCustomer(1, CancellationToken.None));

        Assert.Contains("HTTP 500", exception.Message);
        Assert.DoesNotContain("internal-host", exception.Message);
    }

    [Fact]
    public async Task CreateCustomer_WhenApiFailsWithServerError_TellsClientTheOutcomeIsUnknown()
    {
        var api = new FakeCustomerApi { CreateFailure = new HttpRequestException("boom", null, HttpStatusCode.GatewayTimeout) };

        var exception = await Assert.ThrowsAsync<McpException>(
            () => CreateTools(api).CreateCustomer("Jane", "jane@example.com", CancellationToken.None));

        Assert.Contains("may or may not have been processed", exception.Message);
        Assert.Contains("search_customers", exception.Message);
    }

    [Fact]
    public async Task CreateCustomer_WhenApiRejectsRequest_ReportsRejectionNotUnknownOutcome()
    {
        var api = new FakeCustomerApi { CreateFailure = new HttpRequestException("bad", null, HttpStatusCode.UnprocessableEntity) };

        var exception = await Assert.ThrowsAsync<McpException>(
            () => CreateTools(api).CreateCustomer("Jane", "jane@example.com", CancellationToken.None));

        Assert.Contains("rejected the request", exception.Message);
    }

    [Fact]
    public async Task GetCustomer_WhenApiReturnsNotFoundStatus_ReportsNotFoundMessage()
    {
        var api = new FakeCustomerApi { Failure = new HttpRequestException("nf", null, HttpStatusCode.NotFound) };

        var exception = await Assert.ThrowsAsync<McpException>(() => CreateTools(api).GetCustomer(5, CancellationToken.None));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task GetCustomer_WhenCancelled_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var api = new FakeCustomerApi { Failure = new OperationCanceledException(cts.Token) };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateTools(api).GetCustomer(1, cts.Token));
    }

    [Theory]
    [InlineData("", "jane@example.com")]
    [InlineData("Jane", "not-an-email")]
    public async Task CreateCustomer_WithInvalidArguments_ThrowsMcpException(string name, string email)
    {
        var api = new FakeCustomerApi();

        await Assert.ThrowsAsync<McpException>(() => CreateTools(api).CreateCustomer(name, email, CancellationToken.None));

        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task CreateCustomer_WithValidArguments_ReturnsTheCreatedCustomer()
    {
        var api = new FakeCustomerApi();

        var created = await CreateTools(api).CreateCustomer("  Jane Doe ", "jane@example.com", CancellationToken.None);

        Assert.Equal("Jane Doe", api.LastCreateRequest?.Name);
        Assert.Equal("Jane Doe", created.Name);
    }

    private static CustomerTools CreateTools(ICustomerApiService api) =>
        new(api, NullLogger<CustomerTools>.Instance);

    private sealed class FakeCustomerApi : ICustomerApiService
    {
        public CustomerResponse? Customer { get; init; } = new(1, "Jane Doe", "jane@example.com");

        public Exception? Failure { get; init; }

        public Exception? CreateFailure { get; init; }

        public CreateCustomerRequest? LastCreateRequest { get; private set; }

        public int Calls { get; private set; }

        public Task<CustomerResponse?> GetCustomerAsync(int customerId, CancellationToken cancellationToken)
        {
            Calls++;
            return Failure is null ? Task.FromResult(Customer) : Task.FromException<CustomerResponse?>(Failure);
        }

        public Task<IReadOnlyList<CustomerResponse>> SearchCustomersAsync(string query, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<CustomerResponse>>([]);
        }

        public Task<CustomerResponse> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            LastCreateRequest = request;
            if (CreateFailure is not null)
            {
                return Task.FromException<CustomerResponse>(CreateFailure);
            }

            return Task.FromResult(new CustomerResponse(2, request.Name, request.Email));
        }
    }
}
