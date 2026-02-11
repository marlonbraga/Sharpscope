using MassTransit;
using IntegrationFixtures.Sample.MassTransit.Domain;

namespace IntegrationFixtures.Sample.MassTransit.Infrastructure.Bus;

public sealed class OrderConsumer : IConsumer<OrderSubmitted>
{
    public Task Consume(ConsumeContext<OrderSubmitted> context)
        => Task.CompletedTask;
}
