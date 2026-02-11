using MassTransit;
using IntegrationFixtures.Sample.MassTransit.Domain;

namespace IntegrationFixtures.Sample.MassTransit.Infrastructure.Bus;

public sealed class MassTransitPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync()
        => _publishEndpoint.Publish(new OrderSubmitted("1"));
}
