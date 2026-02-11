using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.HttpInternal.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.HttpInternal.Infrastructure;

public sealed class InternalHttpClientFactory
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public InternalHttpClientFactory(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Configure(IServiceCollection services)
    {
        var baseUrl = _config["HttpClients:UsersApi:BaseUrl"];
        services.AddHttpClient("UsersApi", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });
    }

    public void ConfigureHardcoded(IServiceCollection services)
    {
        services.AddHttpClient("UsersApi", client =>
        {
            client.BaseAddress = new Uri("http://users-api.local");
        });
    }

    public void ConfigureEnv(IServiceCollection services)
    {
        var baseUrl = _secrets.Get("USERS_API_BASE") ?? "http://users-api.local";
        services.AddHttpClient("UsersApiEnv", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });
    }
}
