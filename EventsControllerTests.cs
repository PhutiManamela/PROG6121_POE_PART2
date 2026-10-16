using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests;

public class EventsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EventsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateEvent_AsOrganiser_Returns201()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var request = new CreateEventRequest("City Marathon", "A road race.", DateTime.UtcNow.AddMonths(2), "Cape Town");

        var response = await client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(body);
        Assert.Equal("City Marathon", body!.Name);
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_Returns403()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");
        var request = new CreateEventRequest("Fun Run", "A relaxed run.", DateTime.UtcNow.AddMonths(1), "Pretoria");

        var response = await client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutLogin_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new CreateEventRequest("Trail Run", "Off-road.", DateTime.UtcNow.AddMonths(1), "Hermanus");

        var response = await client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_IsPublic_Returns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_UnknownId_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_NotOwner_Returns403()
    {
        var ownerClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var createResponse = await ownerClient.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Owner's Event", "desc", DateTime.UtcNow.AddMonths(1), "Durban"));
        var created = await createResponse.Content.ReadFromJsonAsync<EventResponse>();

        var otherOrganiserClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var response = await otherOrganiserClient.DeleteAsync($"/api/events/{created!.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
