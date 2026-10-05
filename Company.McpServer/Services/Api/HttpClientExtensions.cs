using System.Net;
using System.Net.Http.Json;

namespace Company.McpServer.Services.Api;

internal static class HttpClientExtensions
{
    /// <summary>
    /// GETs JSON, returning <see langword="null"/> when the API answers 404 so callers can report
    /// "not found" instead of treating it as a failure. Other non-success codes throw.
    /// </summary>
    public static async Task<T?> GetFromJsonOrNullAsync<T>(
        this HttpClient client, string requestUri, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await client.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}
