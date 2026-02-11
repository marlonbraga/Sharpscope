using IntegrationFixtures.Sample.AzureKeyVault.Infrastructure;

namespace IntegrationFixtures.Sample.AzureKeyVault.Application;

public sealed class KeyVaultUseCase
{
    private readonly KeyVaultClientFactory _factory;

    public KeyVaultUseCase(KeyVaultClientFactory factory) => _factory = factory;

    public void Execute()
    {
        _factory.ReadSecret();
        _factory.ReadSecretViaEnv();
    }
}
