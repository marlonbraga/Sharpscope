using IntegrationFixtures.Sample.AzureCosmos.Infrastructure;

namespace IntegrationFixtures.Sample.AzureCosmos.Application;

public sealed class CosmosUseCase
{
    private readonly CosmosRepository _repo;

    public CosmosUseCase(CosmosRepository repo) => _repo = repo;

    public void Execute()
    {
        _repo.Connect();
        _repo.ConnectViaSecret();
    }
}
