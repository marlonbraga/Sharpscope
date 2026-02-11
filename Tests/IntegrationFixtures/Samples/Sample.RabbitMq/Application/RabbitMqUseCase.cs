using IntegrationFixtures.Sample.RabbitMq.Infrastructure;

namespace IntegrationFixtures.Sample.RabbitMq.Application;

public sealed class RabbitMqUseCase
{
    private readonly RabbitMqPublisher _publisher;

    public RabbitMqUseCase(RabbitMqPublisher publisher) => _publisher = publisher;

    public void Execute()
    {
        _publisher.Publish();
        _publisher.Consume();
    }
}
