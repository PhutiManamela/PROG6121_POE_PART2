using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests;

public class ResultsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ResultsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<(EnrolmentResponse enrolment, HttpClient organiserClient)> EnrolParticipantAsync(CustomWebApplicationFactory factory)
    {
        var organiserClient = await TestAuthHelper.CreateAuthenticatedClientAsync(factory, "Organiser");

        var eventResponse = await organiserClient.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Mountain Trail", "desc", DateTime.UtcNow.AddMonths(1), "Table Mountain", 10m, "Run"));
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventResponse>();

        var categoryResponse = await organiserClient.PostAsJsonAsync($"/api/events/{ev!.EventId}/categories",
            new CreateCategoryRequest("15km Trail", 15.00m, 50, 150.00m));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();

        var participantClient = await TestAuthHelper.CreateAuthenticatedClientAsync(factory, "Participant");
        var enrolResponse = await participantClient.PostAsync($"/api/categories/{category!.CategoryId}/enrolments", null);
        var enrolment = await enrolResponse.Content.ReadFromJsonAsync<EnrolmentResponse>();

        return (enrolment!, organiserClient);
    }

    [Fact]
    public async Task CaptureResult_AsOwningOrganiser_Returns201()
    {
        var (enrolment, organiserClient) = await EnrolParticipantAsync(_factory);

        var response = await organiserClient.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new CaptureResultRequest(TimeSpan.FromMinutes(62), 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var (enrolment, organiserClient) = await EnrolParticipantAsync(_factory);

        await organiserClient.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new CaptureResultRequest(TimeSpan.FromMinutes(62), 1));
        var response = await organiserClient.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new CaptureResultRequest(TimeSpan.FromMinutes(65), 2));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_AsParticipant_Returns403()
    {
        var participantClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");

        var response = await participantClient.PostAsJsonAsync("/api/enrolments/1/result",
            new CaptureResultRequest(TimeSpan.FromMinutes(30), 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLeaderboard_IsPublic_Returns200()
    {
        var (enrolment, organiserClient) = await EnrolParticipantAsync(_factory);
        await organiserClient.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result",
            new CaptureResultRequest(TimeSpan.FromMinutes(62), 1));

        var anonymousClient = _factory.CreateClient();
        var response = await anonymousClient.GetAsync($"/api/categories/{enrolment.CategoryId}/leaderboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMyResults_IncludesTotalFinishersForStanding()
    {
        var organiserClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");

        var eventResponse = await organiserClient.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Standing Run", "desc", DateTime.UtcNow.AddMonths(1), "Paarl", 10m, "Run"));
        var ev = await eventResponse.Content.ReadFromJsonAsync<EventResponse>();
        var categoryResponse = await organiserClient.PostAsJsonAsync($"/api/events/{ev!.EventId}/categories",
            new CreateCategoryRequest("10km", 10.00m, 50, 100.00m));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>();

        var first = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");
        var second = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");
        var firstEnrol = await (await first.PostAsync($"/api/categories/{category!.CategoryId}/enrolments", null)).Content.ReadFromJsonAsync<EnrolmentResponse>();
        var secondEnrol = await (await second.PostAsync($"/api/categories/{category.CategoryId}/enrolments", null)).Content.ReadFromJsonAsync<EnrolmentResponse>();

        await organiserClient.PostAsJsonAsync($"/api/enrolments/{firstEnrol!.EnrolmentId}/result", new CaptureResultRequest(TimeSpan.FromMinutes(40), 1));
        await organiserClient.PostAsJsonAsync($"/api/enrolments/{secondEnrol!.EnrolmentId}/result", new CaptureResultRequest(TimeSpan.FromMinutes(45), 2));

        var results = await second.GetFromJsonAsync<List<ResultResponse>>("/api/results/me");

        Assert.Single(results!);
        Assert.Equal(2, results![0].Position);
        Assert.Equal(2, results[0].TotalFinishers);
    }
}
