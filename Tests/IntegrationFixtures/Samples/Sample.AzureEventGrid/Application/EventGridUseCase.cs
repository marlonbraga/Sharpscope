using IntegrationFixtures.Sample.AzureEventGrid.Infrastructure;

namespace IntegrationFixtures.Sample.AzureEventGrid.Application;

public sealed class EventGridUseCase
{
    private readonly EventGridPublisher _publisher;

    public EventGridUseCase(EventGridPublisher publisher) => _publisher = publisher;

    public void Execute()
    {
        _publisher.Publish();
        _publisher.PublishViaSecret();
    }
}
