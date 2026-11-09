using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>
/// Stores event banners and profile pictures in Azure Blob Storage. Only the API talks to Azure:
/// the MVC app forwards the file to the API, and the API uploads it and saves the resulting URL in SQL Server.
/// </summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly string? _connectionString;
    private readonly string _containerName;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        _connectionString = configuration["AzureBlob:ConnectionString"];
        _containerName = configuration["AzureBlob:ContainerName"] ?? "raceday-images";
    }

    public async Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Azure Blob Storage is not configured. Set AzureBlob:ConnectionString.");
        }

        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);

        return blob.Uri.ToString();
    }
}
