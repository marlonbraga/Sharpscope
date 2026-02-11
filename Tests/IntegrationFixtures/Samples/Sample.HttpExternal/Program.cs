using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.HttpExternal.Application;
using IntegrationFixtures.Sample.HttpExternal.Infrastructure;
using IntegrationFixtures.Sample.HttpExternal.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.HttpExternal;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("PAYMENTS_API_BASE", "https://api.payments.example.com");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var factory = new ExternalHttpClientFactory(config, secrets);
        var useCase = new ExternalHttpUseCase(factory);
        useCase.Execute();
    }
}
