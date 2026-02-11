using Microsoft.Extensions.Configuration;
using IntegrationFixtures.Sample.AzureEventGrid.Application;
using IntegrationFixtures.Sample.AzureEventGrid.Infrastructure;
using IntegrationFixtures.Sample.AzureEventGrid.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.AzureEventGrid;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("EVENTGRID_KEY", "EventGridSecretKey==");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var secrets = new EnvSecretProvider();
        var publisher = new EventGridPublisher(config, secrets);
        var useCase = new EventGridUseCase(publisher);
        useCase.Execute();
    }
}
