using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureCosmos.Application;
using IntegrationFixtures.Sample.AzureCosmos.Infrastructure;
using IntegrationFixtures.Sample.AzureCosmos.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureCosmos;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("COSMOS_KEY", "CosmosSecretKey==");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var repo = new CosmosRepository(config, secrets);
        var useCase = new CosmosUseCase(repo);
        useCase.Execute();
    }
}
