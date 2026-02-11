using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureServiceBus.Application;
using IntegrationFixtures.Sample.AzureServiceBus.Infrastructure;
using IntegrationFixtures.Sample.AzureServiceBus.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureServiceBus;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("SERVICEBUS_QUEUE", "orders-queue-secret");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var publisher = new ServiceBusPublisher(config, secrets);
        var useCase = new ServiceBusUseCase(publisher);
        useCase.Execute();
    }
}
