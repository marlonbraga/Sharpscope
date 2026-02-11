using System.Collections.Generic;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using IntegrationFixtures.Sample.Postgres.Domain;
using IntegrationFixtures.Sample.Postgres.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Postgres.Infrastructure;

public sealed class PostgresRepository
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public PostgresRepository(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public IEnumerable<Account> ListAccounts()
    {
        var connectionString = _config.GetConnectionString("PostgresDb");
        using var connection = new NpgsqlConnection(connectionString);
        return connection.Query<Account>("SELECT '1' AS Id, 'demo' AS Name");
    }

    public void Ping()
    {
        var envConn = _secrets.Get("POSTGRES_CONN") ?? string.Empty;
        using var connection = new NpgsqlConnection(envConn);
        connection.Execute("SELECT 1");
    }
}
