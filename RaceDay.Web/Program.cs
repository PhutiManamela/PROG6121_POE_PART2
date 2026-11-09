using System.Net.Http.Headers;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Server-side session: holds the user's JWT and role after login (never exposed to the browser).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "RaceDay.Web.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

// The ONLY way this app reaches data: an HttpClient (created through IHttpClientFactory) pointed at the Part 2 API.
// There is no DbContext, SQL or Azure SDK in this project.
builder.Services.AddHttpClient<IRaceDayApiClient, RaceDayApiClient>(client =>
    {
        var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5100/";
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    })
    // The handler is pooled and shared between users, so it must never keep the API's session cookie:
    // every request authenticates itself with the current user's bearer token instead.
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });

var app = builder.Build();

var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

// Users never see a raw exception page: unhandled errors and bare status codes land on friendly views.
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");

if (!app.Environment.IsDevelopment() && !inContainer)
{
    app.UseHsts();
}

if (!inContainer)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
