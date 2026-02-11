using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureStorage.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureStorage.Infrastructure;

public sealed class BlobStorageClient
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public BlobStorageClient(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Connect()
    {
        var connectionString = _config["Storage:ConnectionString"];
        var client = new BlobServiceClient(connectionString);
        var container = client.GetBlobContainerClient("samples");
        _ = container.GetBlobClient("file.txt");
    }

    public void ConnectViaEnv()
    {
        var connectionString = _secrets.Get("STORAGE_CONN") ?? string.Empty;
        var client = new BlobServiceClient(connectionString);
        _ = client.GetBlobContainerClient("env-samples");
    }
}
