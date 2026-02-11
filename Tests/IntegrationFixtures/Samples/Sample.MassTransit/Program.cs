using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.MassTransit.Application;
using IntegrationFixtures.Sample.MassTransit.Infrastructure;
using IntegrationFixtures.Sample.MassTransit.Infrastructure.Bus;
using IntegrationFixtures.Sample.MassTransit.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.MassTransit;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("MASSTRANSIT_HOST", "rabbitmq://localhost");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        var secrets = new EnvSecretProvider();

        MassTransitConfig.Configure(services, config, secrets);
        services.AddSingleton<MassTransitPublisher>();
        services.AddSingleton<MassTransitUseCase>();

        var provider = services.BuildServiceProvider();
        var useCase = provider.GetRequiredService<MassTransitUseCase>();
        useCase.Execute();
    }
}
