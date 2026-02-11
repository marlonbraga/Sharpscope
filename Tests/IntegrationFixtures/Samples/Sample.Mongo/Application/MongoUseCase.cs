using IntegrationFixtures.Sample.Mongo.Infrastructure;

namespace IntegrationFixtures.Sample.Mongo.Application;

public sealed class MongoUseCase
{
    private readonly MongoRepository _repo;

    public MongoUseCase(MongoRepository repo) => _repo = repo;

    public void Execute()
    {
        _repo.Connect();
        _repo.ConnectViaEnv();
    }
}
