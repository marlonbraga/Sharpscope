using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.HttpInternal.Application;
using IntegrationFixtures.Sample.HttpInternal.Infrastructure;
using IntegrationFixtures.Sample.HttpInternal.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.HttpInternal;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("USERS_API_BASE", "http://users-api.local");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var factory = new InternalHttpClientFactory(config, secrets);
        var useCase = new InternalHttpUseCase(factory);
        useCase.Execute();
    }
}
