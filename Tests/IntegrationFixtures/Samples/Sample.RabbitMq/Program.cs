using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.RabbitMq.Application;
using IntegrationFixtures.Sample.RabbitMq.Infrastructure;
using IntegrationFixtures.Sample.RabbitMq.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.RabbitMq;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("RABBITMQ_QUEUE", "orders.queue.secret");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var publisher = new RabbitMqPublisher(config, secrets);
        var useCase = new RabbitMqUseCase(publisher);
        useCase.Execute();
    }
}
