using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.Mixed.Small.Application;
using IntegrationFixtures.Sample.Mixed.Small.Infrastructure;
using IntegrationFixtures.Sample.Mixed.Small.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Mixed.Small;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("MIXED_QUEUE", "mixed-queue-secret");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var client = new MixedIntegrationClient(config, secrets);
        var useCase = new MixedUseCase(client);
        useCase.Execute();

        using var ctx = new MixedDbContext(config);
        _ = ctx.Database.ProviderName;
    }
}
