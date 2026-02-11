using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using IntegrationFixtures.Sample.RabbitMq.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.RabbitMq.Infrastructure;

public sealed class RabbitMqPublisher
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public RabbitMqPublisher(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Publish()
    {
        var factory = new ConnectionFactory { HostName = _config["RabbitMq:Host"] };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        const string exchange = "orders.exchange";
        var body = Encoding.UTF8.GetBytes("hello");
        channel.BasicPublish(exchange: exchange, routingKey: "orders.created", basicProperties: null, body: body);
    }

    public void Consume()
    {
        var queue = _secrets.Get("RABBITMQ_QUEUE") ?? "orders.queue";
        var factory = new ConnectionFactory { HostName = _config["RabbitMq:Host"] };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        var consumer = new EventingBasicConsumer(channel);
        channel.BasicConsume(queue: queue, autoAck: true, consumer: consumer);
    }
}
