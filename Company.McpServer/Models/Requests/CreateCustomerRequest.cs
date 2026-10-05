using System.ComponentModel;

namespace Company.McpServer.Models.Requests;

public sealed record CreateCustomerRequest
{
    [Description("Full name of the customer.")]
    public required string Name { get; init; }

    [Description("Email address of the customer, e.g. jane.doe@example.com.")]
    public required string Email { get; init; }
}
