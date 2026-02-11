using IntegrationFixtures.Sample.SqlServer.Infrastructure;

namespace IntegrationFixtures.Sample.SqlServer.Application;

public sealed class SqlServerUseCase
{
    private readonly SqlServerRepository _repo;

    public SqlServerUseCase(SqlServerRepository repo) => _repo = repo;

    public void Execute()
    {
        _ = _repo.ListUsers();
        _repo.Ping();
    }
}
