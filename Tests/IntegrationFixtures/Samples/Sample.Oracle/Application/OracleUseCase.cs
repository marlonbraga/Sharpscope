using IntegrationFixtures.Sample.Oracle.Infrastructure;

namespace IntegrationFixtures.Sample.Oracle.Application;

public sealed class OracleUseCase
{
    private readonly OracleRepository _repo;

    public OracleUseCase(OracleRepository repo) => _repo = repo;

    public void Execute()
    {
        _ = _repo.ListEntities();
        _repo.Ping();
    }
}
