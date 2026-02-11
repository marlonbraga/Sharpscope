using IntegrationFixtures.Sample.MassTransit.Infrastructure.Bus;

namespace IntegrationFixtures.Sample.MassTransit.Application;

public sealed class MassTransitUseCase
{
    private readonly MassTransitPublisher _publisher;

    public MassTransitUseCase(MassTransitPublisher publisher)
    {
        _publisher = publisher;
    }

    public void Execute()
    {
        _publisher.PublishAsync().GetAwaiter().GetResult();
    }
}
