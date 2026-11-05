using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WillyShop.Contracts;

namespace WillyShop.IntegrationTests.Infrastructure;

internal sealed class ApiTestClient : IDisposable
{
    private readonly HttpClient _client;
    private string? _csrfToken;

    public ApiTestClient(WillyShopWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    public Task<HttpResponseMessage> GetAsync(string path) => _client.GetAsync(path);

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PutAsync(string path, object? body = null) => SendAsync(HttpMethod.Put, path, body);

    public Task<HttpResponseMessage> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path, null);

    public async Task<CurrentUserDto> RegisterAsync(string suffix)
    {
        var account = $"test{suffix}";
        var response = await PostAsync("/api/v1/auth/register", new RegisterRequest
        {
            Account = account,
            DisplayName = $"測試會員 {suffix}",
            Email = $"{account}@example.test",
            Password = "TestUser123",
            ConfirmPassword = "TestUser123"
        });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>()
            ?? throw new InvalidOperationException("註冊 API 回應為空。");
        var auth = envelope.Data ?? throw new InvalidOperationException(envelope.Message);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        _csrfToken = null;
        return auth.User;
    }

    public static async Task<ApiResponse<T>> ReadEnvelopeAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<ApiResponse<T>>()
            ?? throw new InvalidOperationException("API 回應為空。");

    public void Dispose() => _client.Dispose();

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        await EnsureCsrfTokenAsync();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", _csrfToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private async Task EnsureCsrfTokenAsync()
    {
        if (_csrfToken is not null) return;
        var response = await _client.GetAsync("/api/v1/auth/csrf-token");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>()
            ?? throw new InvalidOperationException("CSRF API 回應為空。");
        _csrfToken = envelope.Data.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("CSRF Token 為空。");
    }
}