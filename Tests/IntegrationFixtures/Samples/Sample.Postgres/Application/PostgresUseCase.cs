using IntegrationFixtures.Sample.Postgres.Infrastructure;

namespace IntegrationFixtures.Sample.Postgres.Application;

public sealed class PostgresUseCase
{
    private readonly PostgresRepository _repo;

    public PostgresUseCase(PostgresRepository repo) => _repo = repo;

    public void Execute()
    {
        _ = _repo.ListAccounts();
        _repo.Ping();
    }
}
