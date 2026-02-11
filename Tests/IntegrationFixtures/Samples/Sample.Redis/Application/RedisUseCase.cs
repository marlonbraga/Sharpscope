using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.Redis.Infrastructure;

namespace IntegrationFixtures.Sample.Redis.Application;

public sealed class RedisUseCase
{
    private readonly RedisCacheClientFactory _factory;

    public RedisUseCase(RedisCacheClientFactory factory) => _factory = factory;

    public void Execute()
    {
        var services = new ServiceCollection();
        _factory.Configure(services);
        _factory.Connect();
    }
}
