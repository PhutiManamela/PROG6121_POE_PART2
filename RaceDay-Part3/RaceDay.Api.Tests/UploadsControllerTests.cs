using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Api.DTOs;
using Xunit;

namespace RaceDay.Api.Tests;

public class UploadsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UploadsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static MultipartFormDataContent ImageForm(string contentType = "image/png", string fileName = "pic.png", int size = 16)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[size]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task<int> CreateEventAsync(HttpClient organiserClient)
    {
        var response = await organiserClient.PostAsJsonAsync("/api/events",
            new CreateEventRequest("Banner Run", "desc", DateTime.UtcNow.AddMonths(1), "Stellenbosch", 21.1m, "Run"));
        var ev = await response.Content.ReadFromJsonAsync<EventResponse>();
        return ev!.EventId;
    }

    [Fact]
    public async Task UploadBanner_AsOwningOrganiser_SavesUrlOnEvent()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var eventId = await CreateEventAsync(client);

        var response = await client.PostAsync($"/api/events/{eventId}/banner", ImageForm());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var upload = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.Contains("/banners/", upload!.Url);

        var ev = await client.GetFromJsonAsync<EventResponse>($"/api/events/{eventId}");
        Assert.Equal(upload.Url, ev!.BannerUrl);
    }

    [Fact]
    public async Task UploadBanner_AsParticipant_Returns403()
    {
        var organiserClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var eventId = await CreateEventAsync(organiserClient);

        var participantClient = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");
        var response = await participantClient.PostAsync($"/api/events/{eventId}/banner", ImageForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_NonImageFile_Returns400()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Organiser");
        var eventId = await CreateEventAsync(client);

        var response = await client.PostAsync($"/api/events/{eventId}/banner", ImageForm("application/pdf", "doc.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutLogin_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/upload", ImageForm());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_ForOwnAccount_SavesUrlOnProfile()
    {
        var email = $"pic.{Guid.NewGuid():N}@example.com";
        var client = _factory.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Pic", "User", email, "StrongPass123", "Participant"));
        var user = await register.Content.ReadFromJsonAsync<AuthResponse>();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "StrongPass123"));

        var response = await client.PostAsync($"/api/users/{user!.UserId}/profile-picture", ImageForm("image/jpeg", "me.jpg"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await client.GetFromJsonAsync<UserProfileResponse>($"/api/users/{user.UserId}");
        Assert.Contains("/profiles/", profile!.ProfilePictureUrl);
    }

    [Fact]
    public async Task UploadProfilePicture_ForAnotherUser_Returns403()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory, "Participant");

        var response = await client.PostAsync("/api/users/999999/profile-picture", ImageForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
