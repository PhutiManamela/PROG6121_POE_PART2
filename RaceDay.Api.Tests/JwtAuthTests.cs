using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests;

/// <summary>Verifies the JWT bearer path used by the Part 3 MVC client (no cookies involved).</summary>
public class JwtAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public JwtAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateCookielessClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private async Task<string> RegisterAndLoginAsync(HttpClient client, string role)
    {
        var email = $"{role.ToLowerInvariant()}.{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Jwt", role, email, "StrongPass123", role));
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "StrongPass123"));
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token!;
    }

    [Fact]
    public async Task Login_ReturnsJwtToken()
    {
        var client = CreateCookielessClient();
        var token = await RegisterAndLoginAsync(client, "Participant");

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task BearerToken_AuthenticatesWithoutCookie()
    {
        var client = CreateCookielessClient();
        var token = await RegisterAndLoginAsync(client, "Organiser");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Bearer Run", "desc", DateTime.UtcNow.AddMonths(1), "Cape Town", 10m, "Run"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BearerToken_WithWrongRole_Returns403()
    {
        var client = CreateCookielessClient();
        var token = await RegisterAndLoginAsync(client, "Participant");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Nope", "desc", DateTime.UtcNow.AddMonths(1), "Durban", 5m, "Walk"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidBearerToken_Returns401()
    {
        var client = CreateCookielessClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Nope", "desc", DateTime.UtcNow.AddMonths(1), "Durban", 5m, "Walk"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
