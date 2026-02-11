using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureServiceBus.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureServiceBus.Infrastructure;

public sealed class ServiceBusPublisher
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public ServiceBusPublisher(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Send()
    {
        var connectionString = _config["ServiceBus:ConnectionString"];
        var client = new ServiceBusClient(connectionString);
        var sender = client.CreateSender("orders-topic");
        sender.SendMessageAsync(new ServiceBusMessage("hello")).GetAwaiter().GetResult();
    }

    public void Receive()
    {
        var connectionString = _config["ServiceBus:ConnectionString"];
        var queueName = _secrets.Get("SERVICEBUS_QUEUE") ?? "orders-queue";
        var client = new ServiceBusClient(connectionString);
        var processor = client.CreateProcessor(queueName);
        processor.ProcessMessageAsync += args => Task.CompletedTask;
    }
}
