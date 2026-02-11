using IntegrationFixtures.Sample.AzureServiceBus.Infrastructure;

namespace IntegrationFixtures.Sample.AzureServiceBus.Application;

public sealed class ServiceBusUseCase
{
    private readonly ServiceBusPublisher _publisher;

    public ServiceBusUseCase(ServiceBusPublisher publisher) => _publisher = publisher;

    public void Execute()
    {
        _publisher.Send();
        _publisher.Receive();
    }
}
