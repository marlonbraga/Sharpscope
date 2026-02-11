using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.Redis.Application;
using IntegrationFixtures.Sample.Redis.Infrastructure;
using IntegrationFixtures.Sample.Redis.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Redis;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("REDIS_CONN", "localhost:6379,password=RedisSecret!");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var factory = new RedisCacheClientFactory(config, secrets);
        var useCase = new RedisUseCase(factory);
        useCase.Execute();
    }
}
