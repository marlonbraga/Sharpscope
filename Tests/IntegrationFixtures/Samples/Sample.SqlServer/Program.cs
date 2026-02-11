using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.SqlServer.Application;
using IntegrationFixtures.Sample.SqlServer.Infrastructure;
using IntegrationFixtures.Sample.SqlServer.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.SqlServer;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("SQLSERVER_CONN", "Server=localhost;Database=SqlServerDb;User Id=sa;Password=SuperSecret123!;");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var repo = new SqlServerRepository(config, secrets);
        var useCase = new SqlServerUseCase(repo);
        useCase.Execute();

        using var ctx = new SqlServerDbContext(config);
        _ = ctx.Database.ProviderName;
    }
}
