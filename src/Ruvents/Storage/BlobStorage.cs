using Azure.Storage.Blobs;

namespace Ruvents.Storage;

/// <summary>
/// Resolves blob containers on demand. Containers are created lazily so a local
/// emulator and a provisioned storage account behave the same way. The client is
/// optional at startup because EF design-time hosts reference only the database.
/// </summary>
internal sealed class BlobStorage(BlobServiceClient? client)
{
    public async Task<BlobContainerClient> GetContainerAsync(string containerName, CancellationToken cancellationToken)
    {
        if (client is null)
        {
            throw new InvalidOperationException(
                "Blob storage is not configured. Start the application through Aspire so the blobs reference supplies an endpoint.");
        }

        var container = client.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return container;
    }
}
