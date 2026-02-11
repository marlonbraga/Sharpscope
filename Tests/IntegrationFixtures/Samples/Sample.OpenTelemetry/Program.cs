using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.OpenTelemetry.Application;
using IntegrationFixtures.Sample.OpenTelemetry.Infrastructure;
using IntegrationFixtures.Sample.OpenTelemetry.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.OpenTelemetry;

public static class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "https://otel.example.com");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        var secrets = new EnvSecretProvider();
        var configurator = new TelemetryConfigurator(config, secrets);
        configurator.Configure(services);

        services.AddSingleton<TelemetryUseCase>();
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TelemetryUseCase>().Execute();
    }
}
