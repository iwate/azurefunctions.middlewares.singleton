using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;

namespace Iwate.AzureFunctions.Middlewares.Singleton;

public class LockService
{
    const string CONTAINER_NAME = "azure-webjobs-hosts";
    private readonly BlobContainerClient _blobContainerClient;
    public LockService(IConfiguration configuration)
    {
        var serviceClient = new BlobServiceClient(configuration["AzureWebJobsStorage"]);
        _blobContainerClient = serviceClient.GetBlobContainerClient(CONTAINER_NAME);
        _blobContainerClient.CreateIfNotExists();
    }

    public async ValueTask<Lock> Lock(string filename, CancellationToken cancellationToken)
    {
        var client = _blobContainerClient.GetBlockBlobClient(filename);
        if (!await client.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await client.UploadAsync(new MemoryStream(Array.Empty<byte>()), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Azure.RequestFailedException ex)
            {
                if (ex.Status != 412)
                    throw;
            }
        }
        var @lock = new Lock(client.GetBlobLeaseClient());
        await @lock.StartAsync(cancellationToken).ConfigureAwait(false);
        return @lock;
    }
}
