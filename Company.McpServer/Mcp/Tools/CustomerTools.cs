using System.ComponentModel;
using Company.McpServer.Models.Requests;
using Company.McpServer.Models.Responses;
using Company.McpServer.Services.Interfaces;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Company.McpServer.Mcp.Tools;

[McpServerToolType]
public sealed class CustomerTools(ICustomerApiService customerApi, ILogger<CustomerTools> logger)
{
    [McpServerTool(Name = "get_customer", ReadOnly = true, Idempotent = true)]
    [Description("Retrieves a single customer by id, returning the customer's id, name and email. " +
                 "Use this when the customer id is already known; use search_customers to find a customer by name or email.")]
    public Task<CustomerResponse> GetCustomer(
        [Description("Numeric id of the customer to retrieve.")] int customerId,
        CancellationToken cancellationToken) =>
        ToolGuard.RunAsync(logger, "get_customer", async () =>
        {
            ToolGuard.RequireId(customerId, nameof(customerId));
            return await customerApi.GetCustomerAsync(customerId, cancellationToken)
                ?? throw new McpException($"Customer {customerId} was not found.");
        }, cancellationToken);

    [McpServerTool(Name = "search_customers", ReadOnly = true, Idempotent = true)]
    [Description("Searches customers by free text (name or email) and returns the matching customers. " +
                 "Returns an empty list when nothing matches. Use this when the customer id is not known.")]
    public Task<IReadOnlyList<CustomerResponse>> SearchCustomers(
        [Description("Text to search for, such as part of a customer's name or email address.")] string query,
        CancellationToken cancellationToken) =>
        ToolGuard.RunAsync(logger, "search_customers", async () =>
        {
            var text = ToolGuard.RequireText(query, nameof(query));
            return await customerApi.SearchCustomersAsync(text, cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "create_customer", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("Creates a new customer and returns the created record including its new id. " +
                 "This changes data: only call it when the user explicitly asks to create a customer, " +
                 "and search_customers first to avoid creating duplicates.")]
    public Task<CustomerResponse> CreateCustomer(
        [Description("Full name of the new customer.")] string name,
        [Description("Email address of the new customer, e.g. jane.doe@example.com.")] string email,
        CancellationToken cancellationToken) =>
        ToolGuard.RunAsync(logger, "create_customer", async () =>
        {
            var request = new CreateCustomerRequest
            {
                Name = ToolGuard.RequireText(name, nameof(name)),
                Email = ToolGuard.RequireEmail(email, nameof(email))
            };
            return await customerApi.CreateCustomerAsync(request, cancellationToken);
        }, cancellationToken, uncertainOutcomeHint: "call search_customers to check whether the customer was created.");
}
