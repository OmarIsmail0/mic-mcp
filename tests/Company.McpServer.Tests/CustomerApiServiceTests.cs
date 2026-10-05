using System.Net;
using System.Text;
using Company.McpServer.Models.Requests;
using Company.McpServer.Services.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace Company.McpServer.Tests;

public sealed class CustomerApiServiceTests
{
    [Fact]
    public async Task GetCustomerAsync_CallsCustomersEndpointAndMapsResponse()
    {
        var handler = new StubHandler(_ => Json("""{"id":7,"name":"Jane Doe","email":"jane@example.com"}"""));
        var service = CreateService(handler);

        var customer = await service.GetCustomerAsync(7, CancellationToken.None);

        Assert.Equal("https://api.test/customers/7", handler.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Equal(new(7, "Jane Doe", "jane@example.com"), customer);
    }

    [Fact]
    public async Task SearchCustomersAsync_EscapesTheQuery()
    {
        var handler = new StubHandler(_ => Json("[]"));
        var service = CreateService(handler);

        var customers = await service.SearchCustomersAsync("jane & co", CancellationToken.None);

        Assert.Empty(customers);
        Assert.Equal("https://api.test/customers?search=jane%20%26%20co", handler.LastRequest?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task CreateCustomerAsync_PostsTheRequestAsJson()
    {
        var handler = new StubHandler(_ => Json("""{"id":1,"name":"Jane Doe","email":"jane@example.com"}""", HttpStatusCode.Created));
        var service = CreateService(handler);

        var created = await service.CreateCustomerAsync(
            new CreateCustomerRequest { Name = "Jane Doe", Email = "jane@example.com" },
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Contains("\"email\":\"jane@example.com\"", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, created.Id);
    }

    [Fact]
    public async Task GetCustomerAsync_WhenApiReturnsNotFound_ReturnsNull()
    {
        var service = CreateService(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        Assert.Null(await service.GetCustomerAsync(99, CancellationToken.None));
    }

    [Fact]
    public async Task GetCustomerAsync_WhenApiReturnsServerError_ThrowsHttpRequestExceptionWithStatusCode()
    {
        var service = CreateService(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetCustomerAsync(99, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
    }

    private static CustomerApiService CreateService(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }, NullLogger<CustomerApiService>.Instance);

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }
}
