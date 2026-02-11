using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.Mongo.Application;
using IntegrationFixtures.Sample.Mongo.Infrastructure;
using IntegrationFixtures.Sample.Mongo.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Mongo;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("MONGO_CONN", "mongodb://user:MongoSecret@localhost:27017");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var repo = new MongoRepository(config, secrets);
        var useCase = new MongoUseCase(repo);
        useCase.Execute();
    }
}
