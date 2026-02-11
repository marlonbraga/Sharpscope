using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.APIM.Application;
using IntegrationFixtures.Sample.APIM.Infrastructure;
using IntegrationFixtures.Sample.APIM.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.APIM;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("APIM_BASE_URL", "https://contoso.azure-api.net");
        Environment.SetEnvironmentVariable("APIM_SUBSCRIPTION_KEY", "ApimSecretKey!");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var factory = new ApimClientFactory(config, secrets);
        var client = new ApimClient(secrets);
        var useCase = new ApimUseCase(factory, client);
        useCase.Execute();
    }
}
