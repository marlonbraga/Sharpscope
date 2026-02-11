using System;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureKeyVault.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureKeyVault.Infrastructure;

public sealed class KeyVaultClientFactory
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public KeyVaultClientFactory(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void ReadSecret()
    {
        var vaultUri = _config["KeyVault:VaultUri"];
        var client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        _ = client.GetSecret("ApiKey");
    }

    public void ReadSecretViaEnv()
    {
        var vaultUri = _config["KeyVault:VaultUri"];
        var secretName = _secrets.Get("KEYVAULT_SECRET_NAME") ?? "ApiKey";
        var client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
        _ = client.GetSecret(secretName);
    }
}
