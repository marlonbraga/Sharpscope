using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.HttpExternal.Infrastructure;

namespace IntegrationFixtures.Sample.HttpExternal.Application;

public sealed class ExternalHttpUseCase
{
    private readonly ExternalHttpClientFactory _factory;

    public ExternalHttpUseCase(ExternalHttpClientFactory factory) => _factory = factory;

    public void Execute()
    {
        var services = new ServiceCollection();
        _factory.Configure(services);
        _factory.ConfigureHardcoded(services);
        _factory.ConfigureEnv(services);
    }
}
