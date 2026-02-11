using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureKeyVault.Application;
using IntegrationFixtures.Sample.AzureKeyVault.Infrastructure;
using IntegrationFixtures.Sample.AzureKeyVault.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureKeyVault;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("KEYVAULT_SECRET_NAME", "ApiKey");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var factory = new KeyVaultClientFactory(config, secrets);
        var useCase = new KeyVaultUseCase(factory);
        useCase.Execute();
    }
}
