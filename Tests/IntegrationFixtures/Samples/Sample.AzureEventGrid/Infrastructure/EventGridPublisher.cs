using System;
using Azure;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureEventGrid.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureEventGrid.Infrastructure;

public sealed class EventGridPublisher
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public EventGridPublisher(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Publish()
    {
        var endpoint = _config["EventGrid:TopicEndpoint"];
        var key = _config["EventGrid:Key"];
        var client = new EventGridPublisherClient(new Uri(endpoint), new AzureKeyCredential(key));
        _ = client;
    }

    public void PublishViaSecret()
    {
        var endpoint = _config["EventGrid:TopicEndpoint"];
        var key = _secrets.Get("EVENTGRID_KEY") ?? string.Empty;
        var client = new EventGridPublisherClient(new Uri(endpoint), new AzureKeyCredential(key));
        _ = client;
    }
}
