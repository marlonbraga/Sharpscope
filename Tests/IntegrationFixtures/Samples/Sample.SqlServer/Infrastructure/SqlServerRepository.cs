using System.Collections.Generic;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.SqlServer.Domain;
using IntegrationFixtures.Sample.SqlServer.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.SqlServer.Infrastructure;

public sealed class SqlServerRepository
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public SqlServerRepository(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public IEnumerable<User> ListUsers()
    {
        var connectionString = _config.GetConnectionString("SqlServerDb");
        using var connection = new SqlConnection(connectionString);
        return connection.Query<User>("SELECT '1' AS Id, 'demo' AS Name");
    }

    public void Ping()
    {
        var envConn = _secrets.Get("SQLSERVER_CONN") ?? string.Empty;
        using var connection = new SqlConnection(envConn);
        connection.Execute("SELECT 1");
    }
}
