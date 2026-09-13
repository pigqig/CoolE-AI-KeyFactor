using System.Net.Http.Headers;
using System.Net.Http.Json;
using KeyFactorDashboard.Models;
using KeyFactorDashboard.Services;

namespace KeyFactorDashboard.Tests;

internal static class PlantAuth
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "ChangeMe!123";

    public static async Task<LoginResponse> LoginAsync(
        HttpClient client,
        string username = AdminUser,
        string password = AdminPassword)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(PythonApiClient.JsonOptions);
        body.ShouldHaveToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return body;
    }

    public static async Task<UserRowDto> CreateUserAsync(
        HttpClient client,
        string username,
        string password,
        string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/users", new CreateUserRequest
        {
            Username = username,
            Password = password,
            DisplayName = username,
            Role = role
        });
        response.EnsureSuccessStatusCode();
        var row = await response.Content.ReadFromJsonAsync<UserRowDto>(PythonApiClient.JsonOptions);
        return row ?? throw new InvalidOperationException("create user returned empty body");
    }

    private static void ShouldHaveToken(this LoginResponse? body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Token))
        {
            throw new InvalidOperationException("login did not return a token");
        }
    }
}
