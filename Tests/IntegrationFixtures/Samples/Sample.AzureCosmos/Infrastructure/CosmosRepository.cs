using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureCosmos.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureCosmos.Infrastructure;

public sealed class CosmosRepository
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public CosmosRepository(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Connect()
    {
        var endpoint = _config["Cosmos:Endpoint"];
        var key = _config["Cosmos:Key"];
        var client = new CosmosClient(endpoint, key);
        _ = client.GetDatabase("sample-db");
    }

    public void ConnectViaSecret()
    {
        var endpoint = _config["Cosmos:Endpoint"];
        var key = _secrets.Get("COSMOS_KEY") ?? string.Empty;
        var client = new CosmosClient(endpoint, key);
        _ = client.GetDatabase("env-db");
    }
}
