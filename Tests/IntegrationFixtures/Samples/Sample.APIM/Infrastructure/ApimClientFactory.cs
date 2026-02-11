using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.APIM.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.APIM.Infrastructure;

public sealed class ApimClientFactory
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public ApimClientFactory(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Configure(IServiceCollection services)
    {
        var baseUrl = _config["Apim:BaseUrl"];
        services.AddHttpClient("Apim", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });
    }

    public void ConfigureEnv(IServiceCollection services)
    {
        var baseUrl = _secrets.Get("APIM_BASE_URL") ?? "https://contoso.azure-api.net";
        services.AddHttpClient("ApimEnv", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });
    }
}
