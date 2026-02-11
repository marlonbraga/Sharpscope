using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.Oracle.Application;
using IntegrationFixtures.Sample.Oracle.Infrastructure;
using IntegrationFixtures.Sample.Oracle.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Oracle;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("ORACLE_CONN", "User Id=system;Password=OracleSecret!;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCL)))");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var repo = new OracleRepository(config, secrets);
        var useCase = new OracleUseCase(repo);
        useCase.Execute();
    }
}
