using System;

namespace IntegrationFixtures.Sample.Mongo.Infrastructure.Secrets;

public interface ISecretProvider
{
    string? Get(string key);
}

public sealed class EnvSecretProvider : ISecretProvider
{
    public string? Get(string key) => Environment.GetEnvironmentVariable(key);
}
