using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using IntegrationFixtures.Sample.Mongo.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Mongo.Infrastructure;

public sealed class MongoRepository
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public MongoRepository(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Connect()
    {
        var conn = _config["Mongo:MongoMain"];
        var client = new MongoClient(conn);
        var db = client.GetDatabase("mongo_db");
        _ = db.ListCollectionNames();
    }

    public void ConnectViaEnv()
    {
        var envConn = _secrets.Get("MONGO_CONN") ?? string.Empty;
        var client = new MongoClient(envConn);
        _ = client.GetDatabase("env_db");
    }
}
