using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.MassTransit.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.MassTransit.Infrastructure.Bus;

public static class MassTransitConfig
{
    public static void Configure(IServiceCollection services, IConfiguration config, ISecretProvider secrets)
    {
        var host = config["MassTransit:Host"] ?? secrets.Get("MASSTRANSIT_HOST") ?? "rabbitmq://localhost";

        services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderConsumer>();

            x.UsingInMemory((context, cfg) =>
            {
                _ = host;
            });
        });
    }
}
