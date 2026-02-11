using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.Mixed.Small.Infrastructure;

namespace IntegrationFixtures.Sample.Mixed.Small.Application;

public sealed class MixedUseCase
{
    private readonly MixedIntegrationClient _client;

    public MixedUseCase(MixedIntegrationClient client) => _client = client;

    public void Execute()
    {
        var services = new ServiceCollection();
        _client.Configure(services);
        _client.SendMessage();
        _client.ReceiveMessage();
    }
}
