using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithValidData_Returns201AndUser()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest("Thandi", "Mokoena", $"thandi.{Guid.NewGuid():N}@example.com", "StrongPass123", "Participant");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal("Participant", body!.Role);
    }

    [Fact]
    public async Task Register_WithMissingPassword_Returns400()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest("Sipho", "Dube", $"sipho.{Guid.NewGuid():N}@example.com", "", "Organiser");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"dup.{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest("Sipho", "Dube", email, "StrongPass123", "Organiser");

        await client.PostAsJsonAsync("/api/auth/register", request);
        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200()
    {
        var client = _factory.CreateClient();
        var email = $"login.{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Lerato", "Khumalo", email, "StrongPass123", "Participant"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "StrongPass123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"wrongpw.{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Kabelo", "Van Wyk", email, "StrongPass123", "Participant"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@example.com", "WhateverPassword"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
