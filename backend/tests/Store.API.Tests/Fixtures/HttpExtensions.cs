using System.Net.Http.Json;
using System.Text.Json;

namespace Store.API.Tests.Fixtures;

public sealed record ErrorBody(string Code, string Message, Dictionary<string, string[]>? Errors);

public static class HttpExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))
        ?? throw new InvalidOperationException($"Response body was empty (status {(int)response.StatusCode}).");

    public static Task<ErrorBody> ReadErrorAsync(this HttpResponseMessage response) =>
        response.ReadJsonAsync<ErrorBody>();
}
