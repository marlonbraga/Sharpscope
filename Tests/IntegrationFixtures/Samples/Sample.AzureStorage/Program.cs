using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureStorage.Application;
using IntegrationFixtures.Sample.AzureStorage.Infrastructure;
using IntegrationFixtures.Sample.AzureStorage.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureStorage;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("STORAGE_CONN", "DefaultEndpointsProtocol=https;AccountName=acc;AccountKey=SuperSecretKey==;EndpointSuffix=core.windows.net");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var client = new BlobStorageClient(config, secrets);
        var useCase = new StorageUseCase(client);
        useCase.Execute();
    }
}
