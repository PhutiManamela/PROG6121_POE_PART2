using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

/// <summary>
/// Boots the real RaceDay.Api app for integration testing, swapping the SQL Server DbContext for an isolated
/// in-memory database and Azure Blob Storage for a fake, so tests need no SQL Server or Azure account.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RaceDayDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            var blobDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBlobStorageService));
            if (blobDescriptor is not null)
            {
                services.Remove(blobDescriptor);
            }

            services.AddSingleton<IBlobStorageService, FakeBlobStorageService>();

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

/// <summary>Stands in for Azure Blob Storage: returns a predictable URL without uploading anything.</summary>
public class FakeBlobStorageService : IBlobStorageService
{
    public Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake.blob.core.windows.net/raceday-images/{blobName}");
}
