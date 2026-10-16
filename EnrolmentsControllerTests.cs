using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests;

public class EnrolmentsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EnrolmentsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<int> CreateEventWithCategoryAsync(HttpClient organiserClient)
    {
        var eventResponse = await organiserClient.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Riverside Run", "desc", DateTime.UtcNow.AddMonths(1), "Riverside Park"));
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventResponse>();

        var categoryResponse = await organiserClient.PostAsJsonAsync($"/api/events/{ev!.EventId}/categories",
            new CreateCategoryRequest("10km", 10.00m, 50, 100.00m));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();

        return category!.CategoryId;
    }

    [Fact]
    public async Task Enrol_AsParticipant_Returns201()
    {
        var organiserClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var categoryId = await CreateEventWithCategoryAsync(organiserClient);

        var participantClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");
        var response = await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.NotNull(body);
        Assert.Equal("Confirmed", body!.Status);
    }

    [Fact]
    public async Task Enrol_Twice_Returns409()
    {
        var organiserClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var categoryId = await CreateEventWithCategoryAsync(organiserClient);

        var participantClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");
        await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);
        var response = await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_AsOrganiser_Returns403()
    {
        var organiserClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var categoryId = await CreateEventWithCategoryAsync(organiserClient);

        var response = await organiserClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMyEnrolments_ReturnsOnlyOwnEnrolments()
    {
        var organiserClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var categoryId = await CreateEventWithCategoryAsync(organiserClient);

        var participantClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");
        await participantClient.PostAsync($"/api/categories/{categoryId}/enrolments", null);

        var response = await participantClient.GetAsync("/api/enrolments/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var enrolments = await response.Content.ReadFromJsonAsync<List<EnrolmentResponse>>();
        Assert.NotNull(enrolments);
        Assert.Single(enrolments!);
    }
}
