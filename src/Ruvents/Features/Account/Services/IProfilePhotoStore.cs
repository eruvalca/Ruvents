namespace Ruvents.Features.Account.Services;

internal interface IProfilePhotoStore
{
    bool TryValidate(string? contentType, long length, out string error);

    Task<string> SaveAsync(string userId, Stream content, string contentType, CancellationToken cancellationToken);

    Task<(string BlobName, string ContentType)?> GetReferenceAsync(string userId, CancellationToken cancellationToken);

    Task<(Stream Content, string ContentType)?> OpenAsync(string blobName, string contentType, CancellationToken cancellationToken);

    Task DeleteAsync(string blobName, CancellationToken cancellationToken);
}
