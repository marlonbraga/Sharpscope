using IntegrationFixtures.Sample.AzureStorage.Infrastructure;

namespace IntegrationFixtures.Sample.AzureStorage.Application;

public sealed class StorageUseCase
{
    private readonly BlobStorageClient _client;

    public StorageUseCase(BlobStorageClient client) => _client = client;

    public void Execute()
    {
        _client.Connect();
        _client.ConnectViaEnv();
    }
}
