namespace RaceDay.Api.Services;

public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a file to Azure Blob Storage and returns its URL.
    /// Throws <see cref="InvalidOperationException"/> if Azure Blob Storage has not been configured.
    /// </summary>
    Task<string> UploadAsync(Stream content, string blobName, string contentType, CancellationToken cancellationToken = default);
}
