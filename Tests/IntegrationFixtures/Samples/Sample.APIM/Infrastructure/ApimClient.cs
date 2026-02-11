using System.Net.Http;
using IntegrationFixtures.Sample.APIM.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.APIM.Infrastructure;

public sealed class ApimClient
{
    private readonly ISecretProvider _secrets;

    public ApimClient(ISecretProvider secrets)
    {
        _secrets = secrets;
    }

    public void ConfigureHeaders()
    {
        var subscriptionKey = _secrets.Get("APIM_SUBSCRIPTION_KEY") ?? string.Empty;
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
    }
}
