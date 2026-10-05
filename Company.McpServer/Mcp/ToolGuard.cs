using System.Net;
using System.Net.Mail;
using System.Text.Json;
using ModelContextProtocol;

namespace Company.McpServer.Mcp;

/// <summary>
/// Shared plumbing for MCP tools: logs each execution and turns failures into
/// <see cref="McpException"/>s whose messages are safe to show to the client.
/// (The MCP SDK only forwards <see cref="McpException"/> messages; everything else is masked.)
/// </summary>
internal static class ToolGuard
{
    /// <param name="uncertainOutcomeHint">
    /// Set for tools that change data. When the call fails in a way that may still have been processed upstream
    /// (timeout, no response, 5xx), the client is told the outcome is unknown and to verify before retrying,
    /// so an LLM does not blindly repeat a non-idempotent write.
    /// </param>
    public static async Task<T> RunAsync<T>(
        ILogger logger,
        string toolName,
        Func<Task<T>> action,
        CancellationToken cancellationToken,
        string? uncertainOutcomeHint = null)
    {
        logger.LogInformation("MCP tool {ToolName} started", toolName);
        try
        {
            var result = await action();
            logger.LogInformation("MCP tool {ToolName} completed", toolName);
            return result;
        }
        catch (McpException ex)
        {
            // Raised by our own validation; the message is already client-safe.
            logger.LogWarning("MCP tool {ToolName} rejected: {Message}", toolName, ex.Message);
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("MCP tool {ToolName} was cancelled by the client", toolName);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Not cancelled by the caller, so the HttpClient timeout elapsed.
            logger.LogWarning(ex, "MCP tool {ToolName} timed out calling the external API", toolName);
            throw new McpException(
                uncertainOutcomeHint is null
                    ? "The external API did not respond in time. Please try again later."
                    : UnknownOutcomeMessage(uncertainOutcomeHint));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
        {
            // Client errors (404, 400, ...) are expected input problems, not server faults: no stack trace.
            logger.LogWarning(
                "MCP tool {ToolName}: external API answered HTTP {StatusCode}", toolName, (int)ex.StatusCode!.Value);
            throw new McpException(DescribeHttpFailure(ex.StatusCode));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(
                ex,
                "MCP tool {ToolName} failed calling the external API (status: {StatusCode})",
                toolName,
                ex.StatusCode is { } status ? (int)status : null);
            throw new McpException(
                uncertainOutcomeHint is null
                    ? DescribeHttpFailure(ex.StatusCode)
                    : UnknownOutcomeMessage(uncertainOutcomeHint));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            logger.LogError(ex, "MCP tool {ToolName} received an unreadable response from the external API", toolName);
            throw new McpException("The external API returned a response that could not be understood.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MCP tool {ToolName} failed unexpectedly", toolName);
            throw;
        }
    }

    public static int RequireId(int value, string parameterName) =>
        value > 0
            ? value
            : throw new McpException($"'{parameterName}' must be a positive integer.");

    public static string RequireText(string? value, string parameterName, int maxLength = 200)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new McpException($"'{parameterName}' is required and cannot be empty.");
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new McpException($"'{parameterName}' must be at most {maxLength} characters.");
    }

    public static string RequireEmail(string? value, string parameterName)
    {
        var email = RequireText(value, parameterName);
        return MailAddress.TryCreate(email, out var address) && address.Address == email
            ? email
            : throw new McpException($"'{parameterName}' must be a valid email address.");
    }

    private static string UnknownOutcomeMessage(string hint) =>
        "The external API did not confirm the request, so it may or may not have been processed. " +
        $"Do not retry yet: {hint}";

    private static string DescribeHttpFailure(HttpStatusCode? statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "The requested resource was not found in the external API.",
        HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity =>
            "The external API rejected the request. Check the arguments and try again.",
        null => "The external API could not be reached. Please try again later.",
        _ => $"The external API returned an error (HTTP {(int)statusCode}). Please try again later."
    };
}
