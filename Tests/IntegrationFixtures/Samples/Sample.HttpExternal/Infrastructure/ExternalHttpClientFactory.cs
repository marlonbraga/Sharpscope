using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.HttpExternal.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.HttpExternal.Infrastructure;

public sealed class ExternalHttpClientFactory
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public ExternalHttpClientFactory(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Configure(IServiceCollection services)
    {
        var baseUrl = _config["HttpClients:Payments:BaseUrl"];
        services.AddHttpClient("Payments", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });
    }

    public void ConfigureHardcoded(IServiceCollection services)
    {
        services.AddHttpClient("Payments", client =>
        {
            client.BaseAddress = new Uri("https://api.payments.example.com");
        });
    }

    public void ConfigureEnv(IServiceCollection services)
    {
        var baseUrl = _secrets.Get("PAYMENTS_API_BASE") ?? "https://api.payments.example.com";
        services.AddHttpClient("PaymentsEnv", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });
    }
}
