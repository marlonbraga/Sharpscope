using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IntegrationFixtures.Sample.Mixed.Small.Infrastructure.Secrets;

namespace IntegrationFixtures.Sample.Mixed.Small.Infrastructure;

public sealed class MixedDbContext : DbContext
{
    private readonly IConfiguration _config;

    public MixedDbContext(IConfiguration config) => _config = config;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = _config.GetConnectionString("MixedDb");
        optionsBuilder.UseSqlServer(connectionString);
    }
}

public sealed class MixedIntegrationClient
{
    private readonly IConfiguration _config;
    private readonly ISecretProvider _secrets;

    public MixedIntegrationClient(IConfiguration config, ISecretProvider secrets)
    {
        _config = config;
        _secrets = secrets;
    }

    public void Configure(IServiceCollection services)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = _config["Redis:Configuration"];
        });

        services.AddHttpClient("MixedApi", client =>
        {
            client.BaseAddress = new Uri(_config["HttpClients:MixedApi:BaseUrl"]);
        });
    }

    public void SendMessage()
    {
        var connection = _config["ServiceBus:ConnectionString"];
        var client = new ServiceBusClient(connection);
        var sender = client.CreateSender("mixed-topic");
        sender.SendMessageAsync(new ServiceBusMessage("hi")).GetAwaiter().GetResult();
    }

    public void ReceiveMessage()
    {
        var connection = _config["ServiceBus:ConnectionString"];
        var queueName = _secrets.Get("MIXED_QUEUE") ?? "mixed-queue";
        var client = new ServiceBusClient(connection);
        var processor = client.CreateProcessor(queueName);
        processor.ProcessMessageAsync += args => Task.CompletedTask;
    }
}
