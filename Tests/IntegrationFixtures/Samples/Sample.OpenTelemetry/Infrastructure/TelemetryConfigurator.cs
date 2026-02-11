using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using IntegrationFixtures.Sample.OpenTelemetry.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.OpenTelemetry.Infrastructure;

public sealed class TelemetryConfigurator
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public TelemetryConfigurator(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Configure(IServiceCollection services)
    {
        var envEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var endpoint = _config["OpenTelemetry:OtlpEndpoint"]
                       ?? envEndpoint
                       ?? _secrets.Get("OTEL_EXPORTER_OTLP_ENDPOINT")
                       ?? "https://otel.example.com";

        services.AddOpenTelemetry()
            .WithTracing(b =>
            {
                b.AddHttpClientInstrumentation();
                b.AddOtlpExporter(o => o.Endpoint = new Uri(endpoint));
            })
            .WithMetrics(b =>
            {
                b.AddHttpClientInstrumentation();
            });
    }
}
