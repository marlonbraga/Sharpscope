using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.APIM.Infrastructure;

namespace IntegrationFixtures.Sample.APIM.Application;

public sealed class ApimUseCase
{
    private readonly ApimClientFactory _factory;
    private readonly ApimClient _client;

    public ApimUseCase(ApimClientFactory factory, ApimClient client)
    {
        _factory = factory;
        _client = client;
    }

    public void Execute()
    {
        var services = new ServiceCollection();
        _factory.Configure(services);
        _factory.ConfigureEnv(services);
        _client.ConfigureHeaders();
    }
}
