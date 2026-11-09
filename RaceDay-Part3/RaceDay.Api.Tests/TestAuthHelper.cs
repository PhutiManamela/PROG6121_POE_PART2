using System.Net.Http.Json;
using RaceDay.Api.DTOs;

namespace RaceDay.Api.Tests;

/// <summary>
/// Creates an HttpClient that is registered and logged in as a fresh user of the given role.
/// WebApplicationFactory's default client preserves cookies across requests, so the session
/// cookie set by /api/auth/login is automatically sent on every subsequent call from this client.
/// </summary>
public static class TestAuthHelper
{
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(CustomWebApplicationFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = $"{role.ToLowerInvariant()}.{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Test", role, email, "StrongPass123", role));
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "StrongPass123"));

        return client;
    }
}
