using Dapper;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using IntegrationFixtures.Sample.Oracle.Domain;
using IntegrationFixtures.Sample.Oracle.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Oracle.Infrastructure;

public sealed class OracleRepository
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public OracleRepository(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public IEnumerable<OracleEntity> ListEntities()
    {
        var connectionString = _config["Oracle:ConnectionString"];
        using var connection = new OracleConnection(connectionString);
        return connection.Query<OracleEntity>("SELECT '1' AS Id FROM dual");
    }

    public void Ping()
    {
        var envConn = _secrets.Get("ORACLE_CONN") ?? string.Empty;
        using var connection = new OracleConnection(envConn);
        connection.Execute("SELECT 1 FROM dual");
    }
}
