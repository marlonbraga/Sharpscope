using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sharpscope.Domain.Models;
using Sharpscope.Infrastructure.Integrations;
using Shouldly;
using Xunit;

namespace Sharpscope.Test.InfrastructureTests.Integrations;

public sealed class IntegrationDiscoveryEngineTests
{
    [Fact(DisplayName = "Detects HttpClient integration from config + graph")]
    public async Task Detects_HttpClient()
    {
        var root = GetFixture("HttpClientSample");
        var graph = BuildGraph(
            externalCalls: new[]
            {
                "Microsoft.Extensions.DependencyInjection.HttpClientFactoryServiceCollectionExtensions.AddHttpClient(string)"
            },
            typeUsages: new[]
            {
                "System.Net.Http.HttpClient"
            });

        var engine = new IntegrationDiscoveryEngine();
        var snapshot = await engine.DiscoverAsync(graph, root, "work", CancellationToken.None);

        var candidate = snapshot.Candidates.Single(c => c.Kind == IntegrationKind.HttpApi);
        candidate.Technology.ShouldBe("HttpClient");
        candidate.LogicalName.ShouldBe("Payments");
        candidate.Endpoint.ShouldNotBeNull();
        candidate.Endpoint!.ShouldContain("https://payments.example", Case.Insensitive);
        candidate.Confidence.ShouldBeGreaterThanOrEqualTo(0.7);
        candidate.Evidence.ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "Detects EF Core SQL Server integration from config + graph")]
    public async Task Detects_EfCore()
    {
        var root = GetFixture("EfCoreSample");
        var graph = BuildGraph(
            externalCalls: new[]
            {
                "Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions.UseSqlServer(string)"
            },
            typeUsages: new[]
            {
                "Microsoft.EntityFrameworkCore.DbContext"
            });

        var engine = new IntegrationDiscoveryEngine();
        var snapshot = await engine.DiscoverAsync(graph, root, "work", CancellationToken.None);

        var candidate = snapshot.Candidates.Single(c => c.Kind == IntegrationKind.Database);
        candidate.LogicalName.ShouldBe("MainDb");
        candidate.Technology.ShouldBe("SqlServer");
        candidate.Endpoint.ShouldNotBeNull();
        candidate.Endpoint!.ShouldContain("Server=localhost");
        candidate.Confidence.ShouldBeGreaterThanOrEqualTo(0.7);
        candidate.Evidence.ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "Detects Redis cache integration from config + graph")]
    public async Task Detects_Redis()
    {
        var root = GetFixture("RedisSample");
        var graph = BuildGraph(
            externalCalls: new[]
            {
                "Microsoft.Extensions.DependencyInjection.RedisCacheServiceCollectionExtensions.AddStackExchangeRedisCache()"
            },
            typeUsages: new[]
            {
                "StackExchange.Redis.ConnectionMultiplexer"
            });

        var engine = new IntegrationDiscoveryEngine();
        var snapshot = await engine.DiscoverAsync(graph, root, "work", CancellationToken.None);

        var candidate = snapshot.Candidates.Single(c => c.Kind == IntegrationKind.Cache);
        candidate.Technology.ShouldBe("Redis");
        candidate.Endpoint.ShouldNotBeNull();
        candidate.Endpoint!.ShouldContain("localhost:6379");
        candidate.Confidence.ShouldBeGreaterThanOrEqualTo(0.7);
        candidate.Evidence.ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "Detects Azure Service Bus integration from config + graph")]
    public async Task Detects_ServiceBus()
    {
        var root = GetFixture("ServiceBusSample");
        var graph = BuildGraph(
            externalCalls: Array.Empty<string>(),
            typeUsages: new[]
            {
                "Azure.Messaging.ServiceBus.ServiceBusClient"
            });

        var engine = new IntegrationDiscoveryEngine();
        var snapshot = await engine.DiscoverAsync(graph, root, "work", CancellationToken.None);

        var candidate = snapshot.Candidates.Single(c => c.Kind == IntegrationKind.MessageBus);
        candidate.Technology.ShouldBe("AzureServiceBus");
        candidate.Endpoint.ShouldNotBeNull();
        candidate.Endpoint!.ShouldContain("Endpoint=sb://");
        candidate.Confidence.ShouldBeGreaterThanOrEqualTo(0.7);
        candidate.Evidence.ShouldNotBeEmpty();
    }

    private static DirectoryInfo GetFixture(string name)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var path = Path.Combine(repoRoot, "Tests", "Sharpscope.Test", "Fixtures", "IntegrationSamples", name);
        return new DirectoryInfo(path);
    }

    private static CodeGraph BuildGraph(IEnumerable<string> externalCalls, IEnumerable<string> typeUsages)
    {
        var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var edges = new List<GraphEdge>();

        var solutionId = GraphIdFactory.CreateSolutionId("IntegrationSamples");
        var projectId = GraphIdFactory.CreateProjectId("integration.csproj");
        var nsId = GraphIdFactory.CreateNamespaceId(projectId, "IntegrationSamples");
        var typeId = GraphIdFactory.CreateTypeId(projectId, "IntegrationSamples.SampleType");
        var methodId = GraphIdFactory.CreateMethodId(typeId, "M():void");

        nodes[solutionId] = new GraphNode(solutionId, GraphNodeKind.Solution, "IntegrationSamples", new Dictionary<string, string>());
        nodes[projectId] = new GraphNode(projectId, GraphNodeKind.Project, "IntegrationSamples", new Dictionary<string, string>());
        nodes[nsId] = new GraphNode(nsId, GraphNodeKind.Namespace, "IntegrationSamples", new Dictionary<string, string>());

        nodes[typeId] = new GraphNode(
            typeId,
            GraphNodeKind.Type,
            "IntegrationSamples.SampleType",
            new Dictionary<string, string>
            {
                [GraphAttributeKeys.DependsOnTypes] = JsonSerializer.Serialize(typeUsages.ToList())
            });

        nodes[methodId] = new GraphNode(
            methodId,
            GraphNodeKind.Method,
            "IntegrationSamples.SampleType.M()",
            new Dictionary<string, string>
            {
                [GraphAttributeKeys.MethodExternalCalls] = JsonSerializer.Serialize(externalCalls.ToList())
            });

        edges.Add(NewEdge(solutionId, projectId));
        edges.Add(NewEdge(projectId, nsId));
        edges.Add(NewEdge(nsId, typeId));
        edges.Add(NewEdge(typeId, methodId));

        return new CodeGraph(nodes, edges);
    }

    private static GraphEdge NewEdge(string from, string to) =>
        new(from, to, GraphEdgeKind.Contains, Label: null, new Dictionary<string, string>(), Evidence: null, Confidence: 1.0);
}
