using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.HttpInternal.Infrastructure;

namespace IntegrationFixtures.Sample.HttpInternal.Application;

public sealed class InternalHttpUseCase
{
    private readonly InternalHttpClientFactory _factory;

    public InternalHttpUseCase(InternalHttpClientFactory factory) => _factory = factory;

    public void Execute()
    {
        var services = new ServiceCollection();
        _factory.Configure(services);
        _factory.ConfigureHardcoded(services);
        _factory.ConfigureEnv(services);
    }
}
