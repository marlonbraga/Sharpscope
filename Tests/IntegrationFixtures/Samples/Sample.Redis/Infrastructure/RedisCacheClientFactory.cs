using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using IntegrationFixtures.Sample.Redis.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Redis.Infrastructure;

public sealed class RedisCacheClientFactory
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public RedisCacheClientFactory(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Configure(IServiceCollection services)
    {
        var redisConfig = _config["Redis:Configuration"];
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConfig;
        });
    }

    public void Connect()
    {
        var envConn = _secrets.Get("REDIS_CONN") ?? string.Empty;
        _ = ConnectionMultiplexer.Connect(envConn);
    }
}
