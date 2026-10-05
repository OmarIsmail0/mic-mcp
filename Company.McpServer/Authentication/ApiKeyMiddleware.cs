using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Company.McpServer.Authentication;

/// <summary>
/// Rejects requests to the MCP endpoint that do not carry the configured <c>X-API-Key</c>.
/// Requests to other paths (for example <c>/health</c>) pass through untouched.
/// </summary>
public sealed class ApiKeyMiddleware(
    RequestDelegate next,
    IOptions<ApiKeyOptions> options,
    ILogger<ApiKeyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(ApiKeyConstants.ProtectedPath))
        {
            await next(context);
            return;
        }

        var expectedKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            // Fail closed: a server without a configured key must never accept requests.
            logger.LogError(
                "MCP API key is not configured. Set '{ConfigKey}' (environment variable '{EnvironmentVariable}'). Rejecting request.",
                $"{ApiKeyConstants.ConfigurationSection}:ApiKey",
                $"{ApiKeyConstants.ConfigurationSection}__ApiKey");
            await RejectAsync(context);
            return;
        }

        var providedKeys = context.Request.Headers[ApiKeyConstants.HeaderName];
        if (providedKeys.Count != 1 || !KeysMatch(providedKeys[0], expectedKey))
        {
            logger.LogWarning(
                "Rejected MCP request from {RemoteIp}: {Reason}.",
                context.Connection.RemoteIpAddress,
                providedKeys.Count == 0 ? "missing API key" : "invalid API key");
            await RejectAsync(context);
            return;
        }

        await next(context);
    }

    // Hashing both values gives equal-length inputs, so the comparison is constant-time
    // and does not leak the length of the expected key.
    private static bool KeysMatch(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }

    private static Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new
        {
            error = "unauthorized",
            message = $"A valid '{ApiKeyConstants.HeaderName}' header is required."
        });
    }
}
