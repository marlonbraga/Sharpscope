using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.Postgres.Application;
using IntegrationFixtures.Sample.Postgres.Infrastructure;
using IntegrationFixtures.Sample.Postgres.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Postgres;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("POSTGRES_CONN", "Host=localhost;Database=PostgresDb;Username=postgres;Password=PostgresSecret!");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var repo = new PostgresRepository(config, secrets);
        var useCase = new PostgresUseCase(repo);
        useCase.Execute();

        using var ctx = new PostgresDbContext(config);
        _ = ctx.Database.ProviderName;
    }
}
